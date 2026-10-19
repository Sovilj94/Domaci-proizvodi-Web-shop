"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { ApiRequestError } from "@/lib/api/client";
import type { ProvjeraPromoKoda } from "@/lib/api/contracts";
import { formatPrice } from "@/lib/utils";

/** Polje za promo kod na checkoutu — kontrolisano; provjeru (query) radi pozivalac. */
export function PromoKodPolje({
  primijenjen,
  provjera,
  onPrimijeni,
  onUkloni,
  greskaSlanja,
}: {
  primijenjen: string | null;
  provjera: { data?: ProvjeraPromoKoda; error: unknown; isFetching: boolean };
  onPrimijeni: (kod: string) => void;
  onUkloni: () => void;
  greskaSlanja?: string;
}) {
  const [unos, setUnos] = useState("");

  const primijeni = () => {
    const kod = unos.trim();
    if (!kod) return;
    onPrimijeni(kod);
    setUnos("");
  };

  const greska = provjera.error
    ? provjera.error instanceof ApiRequestError
      ? provjera.error.message
      : "Provjera koda nije uspjela."
    : greskaSlanja;

  return (
    <div>
      {primijenjen ? (
        <div className="flex items-center justify-between gap-3 rounded-xl bg-cream px-4 py-3 text-sm">
          <span className="min-w-0 truncate">
            <span className="font-medium">
              Promo kod <span translate="no">{provjera.data?.kod ?? primijenjen.toUpperCase()}</span>
            </span>{" "}
            {provjera.isFetching ? (
              <span className="text-muted">Provjeravam…</span>
            ) : provjera.data ? (
              <span className="font-medium text-brand-700">
                −{formatPrice(provjera.data.popust)}
              </span>
            ) : null}
          </span>
          <Button type="button" variant="secondary" onClick={onUkloni}>
            Ukloni
          </Button>
        </div>
      ) : (
        <Field label="Promo kod" htmlFor="promo-kod">
          <div className="flex items-stretch gap-2">
            <Input
              id="promo-kod"
              value={unos}
              onChange={(e) => setUnos(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.preventDefault();
                  primijeni();
                }
              }}
              autoCapitalize="characters"
              maxLength={30}
              className="min-w-0 flex-1"
            />
            <Button type="button" variant="secondary" onClick={primijeni} disabled={!unos.trim()}>
              Primijeni
            </Button>
          </div>
        </Field>
      )}
      {greska && (
        <p role="alert" className="mt-1.5 text-xs text-red-700">
          {greska}
        </p>
      )}
    </div>
  );
}
