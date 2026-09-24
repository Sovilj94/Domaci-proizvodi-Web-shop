"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Mail } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

/** Seconds remaining until `expiresAt`, clamped at 0 so a timestamp already in the
 * past on first render (e.g. a slow navigation) shows the expired state immediately
 * instead of a negative countdown. */
function secondsLeft(expiresAt: string): number {
  return Math.max(0, Math.round((new Date(expiresAt).getTime() - Date.now()) / 1000));
}

function pad(n: number): string {
  return String(n).padStart(2, "0");
}

/**
 * Shown right after a guest submits checkout. No order exists yet — it's created
 * only when the customer clicks the confirmation link mailed to them, and that link
 * expires (`expiresAt`). The countdown is purely informational (the backend is the
 * real clock); it just keeps the customer from thinking the wait is unbounded.
 */
export function CheckEmail({ email, expiresAt }: { email: string; expiresAt: string }) {
  const [remaining, setRemaining] = useState(() => secondsLeft(expiresAt));

  useEffect(() => {
    setRemaining(secondsLeft(expiresAt));
    const id = setInterval(() => setRemaining(secondsLeft(expiresAt)), 1000);
    return () => clearInterval(id);
  }, [expiresAt]);

  const expired = remaining <= 0;

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <div className="rounded-card bg-surface p-8 text-center shadow-card">
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-brand-100 text-brand-600">
          <Mail className="h-9 w-9" />
        </div>
        <h1 className="mt-5 font-display text-2xl font-bold">Provjeri svoj mejl</h1>
        <p className="mt-2 text-muted">
          Poslali smo link za potvrdu narudžbe na{" "}
          <span className="font-semibold text-ink">{email}</span>. Kliknite na njega da
          narudžba bude poslana.
        </p>

        {expired ? (
          <div className="mt-6 space-y-4">
            <p className="rounded-lg bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
              Link je istekao. Pošaljite narudžbu ponovo — korpa je i dalje puna, ništa
              niste izgubili.
            </p>
            <Link href="/checkout" className={cn(buttonVariants())}>
              Nazad na dostavu
            </Link>
          </div>
        ) : (
          <p className="mt-6 font-display text-3xl font-bold tabular-nums text-brand-700">
            {pad(Math.floor(remaining / 60))}:{pad(remaining % 60)}
          </p>
        )}

        <p className="mt-6 text-xs text-muted">
          Narudžba još ne postoji — nastaje tek kad potvrdite mejl.
        </p>
      </div>
    </div>
  );
}
