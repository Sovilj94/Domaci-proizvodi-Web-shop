"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth/auth-context";
import { ApiRequestError } from "@/lib/api/client";
import type { UserRole } from "@/types/domain";

/**
 * Google + Facebook sign-in buttons. Each provider renders only when its public
 * client id is configured (`NEXT_PUBLIC_GOOGLE_CLIENT_ID` /
 * `NEXT_PUBLIC_FACEBOOK_APP_ID`); with neither set the whole block hides, so the
 * feature degrades gracefully until OAuth apps exist. The provider gives us a
 * token client-side, we hand it to the backend, which verifies it and issues our JWT.
 */

const GOOGLE_CLIENT_ID = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;
const FACEBOOK_APP_ID = process.env.NEXT_PUBLIC_FACEBOOK_APP_ID;

interface GoogleCredentialResponse {
  credential?: string;
}
interface GoogleIdApi {
  initialize(cfg: {
    client_id: string;
    callback: (r: GoogleCredentialResponse) => void;
  }): void;
  renderButton(el: HTMLElement, opts: Record<string, unknown>): void;
}
interface FbAuthResponse {
  accessToken: string;
}
interface FbLoginResponse {
  authResponse?: FbAuthResponse;
}
interface FacebookSdk {
  init(cfg: { appId: string; version: string; cookie?: boolean; xfbml?: boolean }): void;
  login(cb: (r: FbLoginResponse) => void, opts?: { scope?: string }): void;
}
declare global {
  interface Window {
    google?: { accounts: { id: GoogleIdApi } };
    FB?: FacebookSdk;
  }
}

function loadScript(src: string, id: string): Promise<void> {
  return new Promise((resolve, reject) => {
    if (document.getElementById(id)) return resolve();
    const s = document.createElement("script");
    s.src = src;
    s.id = id;
    s.async = true;
    s.defer = true;
    s.onload = () => resolve();
    s.onerror = () => reject(new Error("script load failed"));
    document.head.appendChild(s);
  });
}

/** `preporukaKod` (B5): kod iz linka preporuke, kad je dugme na registraciji. */
export function SocialLogin({ preporukaKod }: { preporukaKod?: string } = {}) {
  const router = useRouter();
  const { loginWithGoogle, loginWithFacebook } = useAuth();
  const googleBtnRef = useRef<HTMLDivElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const goHome = useCallback(
    (role: UserRole) => router.push(role === "admin" ? "/admin/dashboard" : "/moj-nalog/narudzbe"),
    [router],
  );

  // Google Identity Services — renders Google's own button.
  useEffect(() => {
    if (!GOOGLE_CLIENT_ID) return;
    let cancelled = false;
    loadScript("https://accounts.google.com/gsi/client", "google-gsi")
      .then(() => {
        if (cancelled || !window.google || !googleBtnRef.current) return;
        window.google.accounts.id.initialize({
          client_id: GOOGLE_CLIENT_ID,
          callback: async (resp) => {
            if (!resp.credential) return;
            setBusy(true);
            setError(null);
            try {
              const user = await loginWithGoogle(resp.credential, preporukaKod);
              goHome(user.role);
            } catch (e) {
              setError(e instanceof ApiRequestError ? e.message : "Google prijava nije uspjela.");
            } finally {
              setBusy(false);
            }
          },
        });
        // GIS wants a fixed pixel width — size it to the container so it never
        // overflows the card on narrow phones (min 200, max 400 per GIS limits).
        const width = Math.min(400, Math.max(200, googleBtnRef.current.offsetWidth || 300));
        window.google.accounts.id.renderButton(googleBtnRef.current, {
          theme: "outline",
          size: "large",
          width,
          text: "continue_with",
        });
      })
      .catch(() => setError("Google prijava trenutno nije dostupna."));
    return () => {
      cancelled = true;
    };
  }, [loginWithGoogle, goHome]);

  // Facebook SDK — we render our own button that triggers FB.login.
  useEffect(() => {
    if (!FACEBOOK_APP_ID) return;
    let cancelled = false;
    loadScript("https://connect.facebook.net/bs_BA/sdk.js", "facebook-sdk")
      .then(() => {
        if (cancelled || !window.FB) return;
        window.FB.init({ appId: FACEBOOK_APP_ID, version: "v19.0", cookie: true, xfbml: false });
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  function facebookLogin() {
    if (!window.FB) return;
    window.FB.login(
      async (resp) => {
        if (!resp.authResponse) return;
        setBusy(true);
        setError(null);
        try {
          const user = await loginWithFacebook(resp.authResponse.accessToken, preporukaKod);
          goHome(user.role);
        } catch (e) {
          setError(e instanceof ApiRequestError ? e.message : "Facebook prijava nije uspjela.");
        } finally {
          setBusy(false);
        }
      },
      { scope: "email,public_profile" },
    );
  }

  if (!GOOGLE_CLIENT_ID && !FACEBOOK_APP_ID) return null;

  return (
    <div className="mt-5">
      <div className="flex items-center gap-3">
        <div className="h-px flex-1 bg-black/10" />
        <span className="text-xs text-muted">ili nastavi preko</span>
        <div className="h-px flex-1 bg-black/10" />
      </div>

      <div className="mt-4 flex flex-col items-center gap-3">
        {GOOGLE_CLIENT_ID && (
          <div ref={googleBtnRef} className="min-h-[40px] w-full max-w-[400px] overflow-hidden" />
        )}
        {FACEBOOK_APP_ID && (
          <button
            type="button"
            onClick={facebookLogin}
            disabled={busy}
            className="flex w-full items-center justify-center gap-2 rounded-lg bg-[#1877F2] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[#166fe0] disabled:opacity-50"
          >
            <svg viewBox="0 0 24 24" fill="currentColor" className="h-4 w-4" aria-hidden="true">
              <path d="M24 12.07C24 5.4 18.63 0 12 0S0 5.4 0 12.07C0 18.1 4.39 23.09 10.13 24v-8.44H7.08v-3.49h3.05V9.41c0-3.02 1.79-4.69 4.53-4.69 1.31 0 2.68.24 2.68.24v2.97h-1.51c-1.49 0-1.95.93-1.95 1.88v2.26h3.32l-.53 3.49h-2.8V24C19.61 23.09 24 18.1 24 12.07" />
            </svg>{" "}
            Nastavi preko Facebooka
          </button>
        )}
      </div>

      {error && (
        <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">
          {error}
        </p>
      )}
    </div>
  );
}
