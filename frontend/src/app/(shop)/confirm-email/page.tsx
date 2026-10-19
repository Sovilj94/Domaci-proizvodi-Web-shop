"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CheckCircle2, Loader2, XCircle } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { useAuth } from "@/lib/auth/auth-context";
import { ApiRequestError } from "@/lib/api/client";

export default function ConfirmEmailPage() {
  const router = useRouter();
  const { confirmEmail } = useAuth();
  const [status, setStatus] = useState<"loading" | "ok" | "error">("loading");
  const [message, setMessage] = useState("");
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current) return; // guard against React strict-mode double run (token is single-use)
    ran.current = true;

    const token = new URLSearchParams(window.location.search).get("token");
    if (!token) {
      setStatus("error");
      setMessage("Nedostaje token za potvrdu.");
      return;
    }
    confirmEmail(token)
      .then(() => {
        setStatus("ok");
        setTimeout(() => router.push("/moj-nalog/narudzbe"), 1400);
      })
      .catch((e) => {
        setStatus("error");
        setMessage(
          e instanceof ApiRequestError ? e.message : "Potvrda nije uspjela. Pokušaj ponovo.",
        );
      });
  }, [confirmEmail, router]);

  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-4 py-20">
      <Logo />
      <div className="mt-6 w-full rounded-card bg-surface p-8 text-center shadow-card">
        {status === "loading" && (
          <>
            <Loader2 className="mx-auto h-8 w-8 animate-spin text-brand-600" />
            <h1 className="mt-5 font-display text-xl font-bold">Potvrđujem email…</h1>
          </>
        )}

        {status === "ok" && (
          <>
            <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-100 text-brand-600">
              <CheckCircle2 className="h-8 w-8" />
            </div>
            <h1 className="mt-5 font-display text-2xl font-bold">Email potvrđen!</h1>
            <p className="mt-2 text-sm text-muted">Prijava je uspjela — preusmjeravam…</p>
          </>
        )}

        {status === "error" && (
          <>
            <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-red-100 text-red-600">
              <XCircle className="h-8 w-8" />
            </div>
            <h1 className="mt-5 font-display text-2xl font-bold">Potvrda nije uspjela</h1>
            <p className="mt-2 text-sm text-muted">{message}</p>
            <div className="mt-6 flex justify-center gap-4 text-sm">
              <Link href="/login" className="font-semibold text-brand-700 hover:underline">
                Prijava
              </Link>
              <Link href="/register" className="font-semibold text-brand-700 hover:underline">
                Registracija
              </Link>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
