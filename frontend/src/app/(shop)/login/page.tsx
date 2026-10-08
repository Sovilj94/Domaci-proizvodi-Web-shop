"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Eye, EyeOff, Lock, Mail } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { Button } from "@/components/ui/button";
import { fieldClass } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { SocialLogin } from "@/components/auth/social-login";
import { useAuth } from "@/lib/auth/auth-context";
import { authApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { cn } from "@/lib/utils";

// useSearchParams() opts the page out of static generation unless it sits below a Suspense
// boundary — without this wrapper `next build` fails at the prerender step for /login.
export default function LoginPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { login } = useAuth();

  // Only an internal path is a safe redirect target — an open `next` is an invitation
  // to bounce a customer to another site right after they type their password.
  const next = searchParams.get("next");
  const safeNext = next && next.startsWith("/") && !next.startsWith("//") ? next : null;
  const registerHref = safeNext ? `/register?next=${encodeURIComponent(safeNext)}` : "/register";
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPw, setShowPw] = useState(false);
  const [remember, setRemember] = useState(true);
  const [forgotNote, setForgotNote] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [unconfirmed, setUnconfirmed] = useState(false);
  const [resent, setResent] = useState(false);
  const [loading, setLoading] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setUnconfirmed(false);
    setResent(false);
    try {
      const user = await login({ email, password });
      router.push(safeNext ?? (user.role === "admin" ? "/admin/dashboard" : "/moj-nalog/narudzbe"));
    } catch (err) {
      if (err instanceof ApiRequestError) {
        setError(err.message);
        setUnconfirmed(err.apiError.code === "email_not_confirmed");
      } else {
        setError("Došlo je do greške.");
      }
    } finally {
      setLoading(false);
    }
  }

  async function resend() {
    setResent(false);
    try {
      await authApi.resendConfirmation(email);
    } finally {
      setResent(true);
    }
  }

  return (
    <div className="grid min-h-[calc(100vh-4rem)] place-items-center px-4 py-10">
      <div className="w-full max-w-sm">
        <div className="rounded-2xl border border-line bg-surface p-8 shadow-lg">
          <div className="flex justify-center">
            <Logo />
          </div>

          <h1 className="mt-6 text-center font-display text-2xl font-bold">Dobrodošli nazad!</h1>
          <p className="mt-1 text-center text-sm text-muted">
            Nemate nalog?{" "}
            <Link href={registerHref} className="font-semibold text-brand-700 hover:underline">
              Registrujte se
            </Link>
          </p>

          <form onSubmit={submit} className="mt-6 space-y-4">
            <div>
              <label htmlFor="email" className="mb-1.5 block text-sm font-medium text-ink">
                Email
              </label>
              <div className="relative">
                <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
                <input
                  id="email"
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="Unesite email adresu"
                  className={cn(fieldClass, "pl-9")}
                />
              </div>
            </div>

            <div>
              <label htmlFor="password" className="mb-1.5 block text-sm font-medium text-ink">
                Lozinka
              </label>
              <div className="relative">
                <Lock className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
                <input
                  id="password"
                  type={showPw ? "text" : "password"}
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Unesite lozinku"
                  className={cn(fieldClass, "px-9")}
                />
                <button
                  type="button"
                  onClick={() => setShowPw((v) => !v)}
                  aria-label={showPw ? "Sakrij lozinku" : "Prikaži lozinku"}
                  className="absolute right-2 top-1/2 grid h-7 w-7 -translate-y-1/2 place-items-center rounded-md text-muted transition hover:text-ink"
                >
                  {showPw ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <div className="flex items-center justify-between text-sm">
              <label className="flex cursor-pointer items-center gap-2 text-muted">
                <input
                  type="checkbox"
                  checked={remember}
                  onChange={(e) => setRemember(e.target.checked)}
                  className="h-4 w-4 accent-brand-600"
                />
                Zapamti me
              </label>
              <button
                type="button"
                onClick={() => setForgotNote(true)}
                className="font-medium text-muted transition hover:text-ink"
              >
                Zaboravljena lozinka?
              </button>
            </div>

            {forgotNote && (
              <p className="rounded-lg bg-sand px-3 py-2 text-xs text-muted">
                Reset lozinke još nije dostupan — kontaktirajte podršku.
              </p>
            )}
            {error && (
              <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">
                {error}
              </p>
            )}
            {unconfirmed && !resent && (
              <button
                type="button"
                onClick={resend}
                className="w-full text-sm font-semibold text-brand-700 hover:underline"
              >
                Pošalji ponovo link za potvrdu
              </button>
            )}
            {resent && (
              <p className="rounded-lg bg-brand-50 px-3 py-2 text-sm font-medium text-brand-700">
                Poslali smo novi link — provjerite email.
              </p>
            )}

            <Button type="submit" size="lg" className="w-full" disabled={loading}>
              {loading ? "Prijava…" : "Prijavi se"}
            </Button>
          </form>

          <SocialLogin />

          <div className="mt-6 flex justify-center gap-5 text-xs text-muted">
            <Link href="/" className="hover:text-ink">
              Politika privatnosti
            </Link>
            <Link href="/" className="hover:text-ink">
              Uslovi korištenja
            </Link>
          </div>
        </div>

        <div className="mt-4 rounded-xl bg-honey-100 p-3 text-xs text-ink/70">
          <p className="font-semibold">Demo nalozi</p>
          <p>Admin: admin@epiljara.ba / admin123</p>
          <p>Kupac: mujo@example.com / mujo123</p>
        </div>
      </div>
    </div>
  );
}
