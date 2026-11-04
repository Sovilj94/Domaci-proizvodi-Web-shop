"use client";

import { Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { CheckCircle2 } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { authApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";

export default function OdjavaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Odjava />
    </Suspense>
  );
}

/**
 * Odjava sa ručnih obavještenja i novosti (link iz mejla). Traži klik, ne odjavljuje pri otvaranju —
 * mejl skeneri otvaraju linkove sami, pa bi kupac bio odjavljen a da nije ni kliknuo.
 */
function Odjava() {
  const params = useSearchParams();
  const email = params.get("e") ?? "";
  const potpis = params.get("p") ?? "";
  const [gotovo, setGotovo] = useState(false);
  const [greska, setGreska] = useState<string | null>(null);
  const [salje, setSalje] = useState(false);

  async function odjavi() {
    setSalje(true);
    setGreska(null);
    try {
      await authApi.odjavaSaObavjestenja(email, potpis);
      setGotovo(true);
    } catch (err) {
      setGreska(err instanceof ApiRequestError ? err.message : "Odjava nije uspjela. Pokušaj ponovo.");
    } finally {
      setSalje(false);
    }
  }

  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-4 py-20">
      <Logo />
      <div className="mt-6 w-full rounded-card bg-surface p-8 text-center shadow-card">
        {gotovo ? (
          <>
            <div className="mx-auto grid h-14 w-14 place-items-center rounded-full bg-brand-100 text-brand-600">
              <CheckCircle2 className="h-8 w-8" />
            </div>
            <h1 className="mt-5 font-display text-2xl font-bold">Odjavljeni ste</h1>
            <p className="mt-2 text-sm text-muted">
              Više vam nećemo slati obavještenja i novosti. Mejlovi o vašim narudžbama i dalje stižu.
            </p>
          </>
        ) : (
          <>
            <h1 className="font-display text-2xl font-bold">Odjava sa obavještenja</h1>
            <p className="mt-2 text-sm text-muted">
              {email ? (
                <>
                  Adresa <b className="text-ink">{email}</b> više neće dobijati obavještenja i novosti E piljare.
                </>
              ) : (
                "Link nije potpun — otvorite ga direktno iz mejla."
              )}
            </p>
            {greska && <p className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">{greska}</p>}
            {email && potpis && (
              <Button onClick={odjavi} disabled={salje} className="mt-6 w-full">
                {salje ? "Odjavljujem…" : "Odjavi me"}
              </Button>
            )}
          </>
        )}
        <Link href="/" className="mt-6 inline-block text-sm font-semibold text-brand-700 hover:underline">
          Na početnu
        </Link>
      </div>
    </div>
  );
}
