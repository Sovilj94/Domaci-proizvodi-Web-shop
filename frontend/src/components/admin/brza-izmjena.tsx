"use client";

import { useEffect, useRef, useState } from "react";
import { MJESECI_NAZIVI, MJESECI_SLOVA, prebaciMjesec } from "@/lib/admin/proizvodi-tabela";
import { cn } from "@/lib/utils";

/**
 * Broj koji se mijenja na mjestu: klik ga pretvara u polje, Enter ili izlazak iz polja čuva,
 * Esc odustaje. Prazno polje znači „nema vrijednosti" samo kad je `dozvoliPrazno`.
 */
export function BrojUTabeli({
  vrijednost,
  prikaz,
  onSacuvaj,
  oznaka,
  korak = 0.01,
  dozvoliPrazno = false,
  className,
}: {
  vrijednost: number | null | undefined;
  prikaz: React.ReactNode;
  onSacuvaj: (nova: number | null) => void;
  oznaka: string;
  korak?: number;
  dozvoliPrazno?: boolean;
  className?: string;
}) {
  const [uredjuje, setUredjuje] = useState(false);
  const [unos, setUnos] = useState("");
  const polje = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (uredjuje) polje.current?.select();
  }, [uredjuje]);

  function pocni() {
    setUnos(vrijednost == null ? "" : String(vrijednost));
    setUredjuje(true);
  }

  function zavrsi(cuvaj: boolean) {
    setUredjuje(false);
    if (!cuvaj) return;
    const t = unos.trim().replace(",", ".");
    if (t === "") {
      if (dozvoliPrazno && vrijednost != null) onSacuvaj(null);
      return;
    }
    const n = Number(t);
    if (!Number.isFinite(n) || n < 0 || n === vrijednost) return;
    onSacuvaj(n);
  }

  if (uredjuje)
    return (
      <input
        ref={polje}
        type="number"
        inputMode="decimal"
        step={korak}
        min={0}
        value={unos}
        aria-label={oznaka}
        onChange={(e) => setUnos(e.target.value)}
        onBlur={() => zavrsi(true)}
        onKeyDown={(e) => {
          if (e.key === "Enter") zavrsi(true);
          if (e.key === "Escape") zavrsi(false);
        }}
        className={cn(
          "w-20 rounded-md border border-brand-500 bg-white px-1.5 py-0.5 text-right text-sm tabular-nums outline-none ring-2 ring-brand-500/20",
          className,
        )}
      />
    );

  return (
    <button
      type="button"
      onClick={pocni}
      title={`${oznaka} — klikni za izmjenu`}
      className={cn(
        "rounded-md px-1.5 py-0.5 text-right tabular-nums transition hover:bg-brand-50 hover:ring-1 hover:ring-brand-200",
        className,
      )}
    >
      {prikaz}
    </button>
  );
}

/** Dvanaest mjeseci kao dugmad; uključen = artikal je u sezoni. Prazan niz = cijele godine. */
export function MjeseciTraka({
  mjeseci,
  tekuci,
  onPromijeni,
  disabled,
}: {
  mjeseci: number[] | undefined;
  tekuci: number;
  onPromijeni: (novi: number[]) => void;
  disabled?: boolean;
}) {
  const cijelaGodina = !mjeseci || mjeseci.length === 0;
  return (
    <div className="flex gap-px" role="group" aria-label="Mjeseci sezone">
      {MJESECI_SLOVA.map((slovo, i) => {
        const m = i + 1;
        const ukljucen = cijelaGodina || mjeseci!.includes(m);
        return (
          <button
            key={m}
            type="button"
            disabled={disabled}
            aria-pressed={ukljucen}
            title={`${MJESECI_NAZIVI[i]}${ukljucen ? " — u sezoni" : ""}${cijelaGodina ? " (cijele godine)" : ""}`}
            onClick={() => onPromijeni(prebaciMjesec(mjeseci, m))}
            className={cn(
              "h-6 w-[18px] text-[10px] font-semibold transition first:rounded-l last:rounded-r disabled:opacity-50",
              ukljucen
                ? cijelaGodina
                  ? "bg-brand-100 text-brand-700 hover:bg-brand-200"
                  : "bg-brand-500 text-white hover:bg-brand-600"
                : "bg-zinc-100 text-zinc-400 hover:bg-zinc-200",
              m === tekuci && "outline outline-2 -outline-offset-2 outline-honey-500",
            )}
          >
            {slovo}
          </button>
        );
      })}
    </div>
  );
}

export function Prekidac({
  ukljucen,
  onPromijeni,
  oznaka,
  disabled,
}: {
  ukljucen: boolean;
  onPromijeni: (novo: boolean) => void;
  oznaka: string;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={ukljucen}
      aria-label={oznaka}
      title={oznaka}
      disabled={disabled}
      onClick={() => onPromijeni(!ukljucen)}
      className={cn(
        "relative h-5 w-9 shrink-0 rounded-full transition disabled:opacity-50",
        ukljucen ? "bg-brand-500" : "bg-zinc-300",
      )}
    >
      <span
        className={cn(
          "absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-all",
          ukljucen ? "left-[18px]" : "left-0.5",
        )}
      />
    </button>
  );
}
