"use client";

import Image from "next/image";
import { useState } from "react";
import type { DodatnaKultura, StavkaSadrzaja } from "@/types/domain";

/**
 * Obojena kartica namirnice iz sadržaja korpice (R2, po uzoru na farmit): naziv i količina,
 * 📍 selo, 🧑‍🌾 domaćin, slika desno. Boja dolazi sa servera (`BojaProizvoda`).
 * Slika koja ne postoji se sakrije — ostaje kartica bez polomljene ikone.
 */
export function StavkaSadrzajaKartica({ s }: { s: StavkaSadrzaja }) {
  const [pala, setPala] = useState(false);

  return (
    <div
      className="flex min-h-[92px] items-center gap-3 rounded-2xl px-4 py-3 text-ink"
      style={{ backgroundColor: s.boja ?? "var(--color-beige)" }}
    >
      <div className="min-w-0 flex-1">
        <p className="text-base font-semibold leading-tight">
          {s.naziv}{" "}
          <span className="text-xs font-normal">
            {s.kolicina} {s.jedinica}
          </span>
        </p>
        <p className="mt-1.5 truncate text-xs">📍 {s.proizvodjacSelo}</p>
        <p className="truncate text-xs">🧑‍🌾 {s.proizvodjacNaziv}</p>
      </div>
      {s.imageUrl && !pala && (
        <div className="relative h-16 w-20 shrink-0 overflow-hidden rounded-xl">
          <Image src={s.imageUrl} alt="" fill sizes="80px" className="object-cover" onError={() => setPala(true)} />
        </div>
      )}
    </div>
  );
}

/** Mala obojena pločica dodatne kulture: sličica i naziv, bez količine — nju računamo pri zamjeni. */
export function DodatnaKulturaPlocica({ d }: { d: DodatnaKultura }) {
  const [pala, setPala] = useState(false);

  return (
    <div
      className="flex items-center gap-2 rounded-full py-1 pl-1 pr-3 text-sm text-ink"
      style={{ backgroundColor: d.boja ?? "var(--color-beige)" }}
    >
      {d.imageUrl && !pala ? (
        <span className="relative h-7 w-7 shrink-0 overflow-hidden rounded-full">
          <Image src={d.imageUrl} alt="" fill sizes="28px" className="object-cover" onError={() => setPala(true)} />
        </span>
      ) : (
        <span className="h-7 w-1" aria-hidden />
      )}
      {d.naziv}
    </div>
  );
}
