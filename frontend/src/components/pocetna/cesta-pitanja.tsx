"use client";

import { ChevronDown } from "lucide-react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { useKorpiceList, useSadrzajSajta, useSedmica } from "@/lib/api/hooks";
import { CESTA_PITANJA, kontekstPitanja, popuniOdgovor } from "@/lib/cesta-pitanja";

/**
 * Česta pitanja (R1, po uzoru na farmit): krem pojas sa lukom, harmonika bez okvira, red
 * po pitanju sa linijom ispod. Harmonika je na <details>/<summary>: radi i bez JS-a i čitač
 * ekrana je zna sam. Odgovori čitaju brojeve sa servera kroz `kontekstPitanja`.
 */
export function CestaPitanja() {
  const { data: sedmica } = useSedmica();
  const { data: korpice } = useKorpiceList();
  const { data: sadrzaj } = useSadrzajSajta();
  const kontekst = kontekstPitanja(sedmica, korpice);
  // Pitanja iz admin panela imaju prednost; dok ih admin nije unio, važe ugrađena.
  const pitanja = sadrzaj?.pitanja?.length
    ? sadrzaj.pitanja.map((p) => ({ pitanje: p.pitanje, odgovor: popuniOdgovor(p.odgovor, kontekst) }))
    : CESTA_PITANJA.map((p) => ({ pitanje: p.pitanje, odgovor: p.odgovor(kontekst) }));

  return (
    <Sekcija ton="krem" luk className="pb-28 sm:pb-36">
      <SectionHeader
        naslov="Česta pitanja 🙋‍♀️"
        opis="Ovdje su odgovori na ono što nas kupci najčešće pitaju. Ako nešto nedostaje, javi nam se."
      />
      <div className="mx-auto max-w-3xl">
        {pitanja.map((p) => (
          <details key={p.pitanje} className="group border-b border-line">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-4 py-5 text-base font-bold text-ink sm:text-lg [&::-webkit-details-marker]:hidden">
              {p.pitanje}
              <ChevronDown className="h-5 w-5 shrink-0 text-ink transition group-open:rotate-180" />
            </summary>
            <p className="pb-5 text-[15px] leading-relaxed text-ink/80">{p.odgovor}</p>
          </details>
        ))}
      </div>
    </Sekcija>
  );
}
