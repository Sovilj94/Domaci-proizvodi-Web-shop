"use client";

import Image from "next/image";
import { useState } from "react";
import { SlikaKulture } from "@/components/korpice/slika-kulture";
import { cn } from "@/lib/utils";
import type { DodatnaKultura, StavkaSadrzaja } from "@/types/domain";

/**
 * Obojena kartica namirnice iz sadržaja korpice (R2, po uzoru na farmit): naziv i količina,
 * 📍 selo, 🧑‍🌾 domaćin, izrez namirnice desno (bez izreza fotografija). Boja dolazi sa servera
 * (`BojaProizvoda`). Dodatne kulture koriste istu karticu bez količine (nju računamo tek pri zamjeni).
 */
export function StavkaSadrzajaKartica({
  s,
}: {
  s: Pick<StavkaSadrzaja, "naziv" | "jedinica" | "imageUrl" | "izrezUrl" | "boja"> & {
    kolicina?: number;
    proizvodjacNaziv?: string;
    proizvodjacSelo?: string;
  };
}) {
  return (
    <div
      className="flex min-h-[92px] items-center gap-3 rounded-2xl px-4 py-3 text-ink"
      style={{ backgroundColor: s.boja ?? "var(--color-beige)" }}
    >
      <div className="min-w-0 flex-1">
        <p className="text-base font-semibold leading-tight">
          {s.naziv}{" "}
          {s.kolicina != null && (
            <span className="text-xs font-normal">
              {s.kolicina} {s.jedinica}
            </span>
          )}
        </p>
        {s.proizvodjacSelo && <p className="mt-1.5 truncate text-xs">📍 {s.proizvodjacSelo}</p>}
        {s.proizvodjacNaziv && <p className="truncate text-xs">🧑‍🌾 {s.proizvodjacNaziv}</p>}
      </div>
      <SlikaKulture izrezUrl={s.izrezUrl} imageUrl={s.imageUrl} sizes="80px" className="h-16 w-20 shrink-0" />
    </div>
  );
}

/** Mala obojena pločica dodatne kulture: sličica i naziv, bez količine — nju računamo pri zamjeni. */
export function DodatnaKulturaPlocica({ d }: { d: DodatnaKultura }) {
  const [pala, setPala] = useState(false);
  const src = d.izrezUrl || d.imageUrl;

  return (
    <div
      className="flex items-center gap-2 rounded-full py-1 pl-1 pr-3 text-sm text-ink"
      style={{ backgroundColor: d.boja ?? "var(--color-beige)" }}
    >
      {src && !pala ? (
        <span className={cn("relative h-7 w-7 shrink-0 overflow-hidden", !d.izrezUrl && "rounded-full")}>
          <Image
            src={src}
            alt=""
            fill
            sizes="28px"
            className={d.izrezUrl ? "object-contain" : "object-cover"}
            onError={() => setPala(true)}
          />
        </span>
      ) : (
        <span className="h-7 w-1" aria-hidden />
      )}
      {d.naziv}
    </div>
  );
}
