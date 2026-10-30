"use client";

import { useBlokovi } from "@/lib/api/hooks";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";

/**
 * Tri koraka (R1, po uzoru na farmit): bijeli krug sa emoji, isprekidana linija između,
 * naslov i tekst; na mobilnom vertikalno sa ikonom lijevo. Namjerno bez brojeva (rok, dan,
 * cijena) — oni dolaze sa servera na drugim mjestima (spec P4, O12).
 */

/** Koraci su u bazi (sekcija „pocetna-koraci"); bez njih se sekcija ne prikazuje. */
export function KakoFunkcionise() {
  const { blokovi: koraci } = useBlokovi("pocetna-koraci");
  if (koraci.length === 0) return null;
  return (
    <Sekcija ton="krem" luk>
      <SectionHeader naslov="Kako funkcioniše?" />
      <ol className="relative grid gap-8 md:grid-cols-3 md:gap-6">
        {/* Isprekidana linija iza krugova, samo na desktopu. */}
        <div
          className="absolute left-[16.6%] right-[16.6%] top-10 hidden border-t-[3px] border-dashed border-white md:block"
          aria-hidden
        />
        {koraci.map((k) => (
          <li key={k.id} className="relative flex gap-5 md:flex-col md:items-center md:text-center">
            <span className="grid h-20 w-20 shrink-0 place-items-center rounded-full bg-white text-4xl shadow-sm" aria-hidden>
              {k.ikona}
            </span>
            <div>
              <h3 className="text-2xl font-semibold text-ink md:mt-4 md:text-[28px]">{k.naslov}</h3>
              <p className="mt-2 text-sm leading-relaxed text-ink/80 md:mx-auto md:max-w-[270px]">{k.tekst}</p>
            </div>
          </li>
        ))}
      </ol>
    </Sekcija>
  );
}
