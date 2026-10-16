"use client";

import Image from "next/image";
import { Karusel } from "@/components/pocetna/karusel";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { useUtisci } from "@/lib/api/hooks";
import { IZGLED } from "@/lib/izgled";

/**
 * Utisci kupaca kao karusel (R1, po uzoru na farmit „Rekli su o farmitu"): velika fotografija
 * lijevo, desno redni broj, ime i tekst. Utisak nema svoju fotografiju, pa se smjenjuju
 * fotografije iz `IZGLED.utisci`. Bez objavljenih utisaka sekcija se ne prikazuje (spec P4, O2).
 */
export function UtisciKarusel() {
  const { data } = useUtisci();
  if (!data?.length) return null;

  return (
    <Sekcija>
      <SectionHeader naslov="Rekli su o E piljari" />
      <Karusel stavka="w-full">
        {data.slice(0, 8).map((u, i) => (
          <figure key={u.id} className="grid gap-6 sm:grid-cols-[320px_1fr] sm:gap-10">
            <div className="relative mx-auto aspect-[4/5] w-full max-w-[320px] overflow-hidden rounded-[26px] sm:mx-0">
              <Image
                src={IZGLED.utisci[i % IZGLED.utisci.length]}
                alt=""
                fill
                sizes="(max-width: 640px) 90vw, 320px"
                className="object-cover"
              />
            </div>
            <div className="sm:pt-2">
              <p className="text-3xl font-bold text-ink">#{i + 1}</p>
              <figcaption className="text-2xl font-semibold text-ink sm:text-3xl">
                {u.ime}
                {u.mjesto && <span className="block text-sm font-normal text-muted">{u.mjesto}</span>}
              </figcaption>
              <blockquote className="mt-4 text-[15px] leading-relaxed text-ink">{u.tekst}</blockquote>
            </div>
          </figure>
        ))}
      </Karusel>
    </Sekcija>
  );
}
