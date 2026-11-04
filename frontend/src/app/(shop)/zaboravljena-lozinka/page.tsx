"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { MailCheck, Mail } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { Button } from "@/components/ui/button";
import { fieldClass } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { authApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { cn } from "@/lib/utils";

export default function ZaboravljenaLozinkaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Forma />
    </Suspense>
  );
}

/** Mejl sa linkom za novu lozinku. Odgovor je isti bez obzira da li nalog postoji. */
function Forma() {
  const params = useSearchParams();
  const [email, setEmail] = useState(params.get("email") ?? "");
  const [poslato, setPoslato] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);
  const [salje, setSalje] = useState(false);

  async function posalji(e: React.FormEvent) {
    e.preventDefault();
    setSalje(true);
    setGreska(null);
    try {
      await authApi.zaboravljenaLozinka(email.trim());
      setPoslato(true);
    } catch (err) {
      setGreska(err instanceof ApiRequestError ? err.message : "Slanje nije uspjelo. Pokušaj ponovo.");
    } finally {
      setSalje(false);
    }
  }

  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-4 py-20">
      <Logo />
      <div className="mt-6 w-full rounded-card bg-surface p-8 shadow-card">
        {poslato ? (
          <div className="text-center">
            <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-100 text-brand-600">
              <MailCheck className="h-8 w-8" />
            </div>
            <h1 className="mt-5 font-display text-2xl font-bold">Provjeri mejl</h1>
            <p className="mt-2 text-sm text-muted">
              Ako nalog sa adresom <b className="text-ink">{email.trim()}</b> postoji, poslali smo link za novu lozinku.
              Link važi 1 sat. Ne vidiš ga? Pogledaj i u spamu.
            </p>
            <Link href="/login" className="mt-6 inline-block text-sm font-semibold text-brand-700 hover:underline">
              Nazad na prijavu
            </Link>
          </div>
        ) : (
          <form onSubmit={posalji} className="space-y-4">
            <div>
              <h1 className="font-display text-2xl font-bold">Zaboravljena lozinka</h1>
              <p className="mt-1 text-sm text-muted">Upiši adresu naloga — poslaćemo ti link za novu lozinku.</p>
            </div>
            <div className="relative">
              <Mail className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <input
                type="email"
                required
                autoFocus
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Email adresa"
                aria-label="Email adresa"
                className={cn(fieldClass, "pl-9")}
              />
            </div>
            {greska && <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">{greska}</p>}
            <Button type="submit" className="w-full" disabled={salje}>
              {salje ? "Šaljem…" : "Pošalji link"}
            </Button>
            <p className="text-center text-sm text-muted">
              Sjetio si se?{" "}
              <Link href="/login" className="font-semibold text-brand-700 hover:underline">
                Prijavi se
              </Link>
            </p>
          </form>
        )}
      </div>
    </div>
  );
}
