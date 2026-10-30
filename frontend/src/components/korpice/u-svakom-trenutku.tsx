"use client";

import Link from "next/link";
import { useBlokovi } from "@/lib/api/hooks";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";

/**
 * „U svakom trenutku možeš da:" (po uzoru na farmit): šta kupac sam podešava u Mom nalogu.
 * Stavke su u bazi (sekcija „korpice-mogucnosti"); bez njih se sekcija ne prikazuje.
 */
export function USvakomTrenutku() {
  const { blokovi: mogucnosti } = useBlokovi("korpice-mogucnosti");
  if (mogucnosti.length === 0) return null;
  return (
    <Sekcija ton="krem">
      <SectionHeader
        naslov="U svakom trenutku možeš da:"
        opis="Iz svog naloga upravljaš pretplatom i korpicom jednim klikom."
      />
      <ul className="grid grid-cols-2 gap-x-4 gap-y-8 sm:grid-cols-3 lg:grid-cols-6">
        {mogucnosti.map((m) => (
          <li key={m.id} className="flex flex-col items-center text-center">
            <span className="grid h-16 w-16 place-items-center rounded-full bg-white text-3xl shadow-sm" aria-hidden>
              {m.ikona}
            </span>
            <h3 className="mt-3 text-xl font-semibold text-ink">{m.naslov}</h3>
            <p className="mt-1 max-w-[170px] text-sm text-ink/80">{m.tekst}</p>
          </li>
        ))}
      </ul>
      <p className="mt-10 text-center">
        <Link href="/moj-nalog" className="text-sm font-medium text-brand-600 underline underline-offset-2">
          Otvori Moj nalog
        </Link>
      </p>
    </Sekcija>
  );
}
