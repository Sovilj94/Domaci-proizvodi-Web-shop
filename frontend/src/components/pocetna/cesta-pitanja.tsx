"use client";

import { ChevronDown } from "lucide-react";
import { SectionHeader } from "@/components/ui/section-header";
import { useKorpiceList, useSedmica } from "@/lib/api/hooks";
import { CESTA_PITANJA, kontekstPitanja } from "@/lib/cesta-pitanja";

/**
 * Harmonika na <details>/<summary>: otvara se i bez JS-a i čitač ekrana je zna sam.
 * Odgovori čitaju brojeve sa servera kroz `kontekstPitanja`.
 */
export function CestaPitanja() {
  const { data: sedmica } = useSedmica();
  const { data: korpice } = useKorpiceList();
  const kontekst = kontekstPitanja(sedmica, korpice);

  return (
    <>
      <SectionHeader naslov="Česta pitanja" />
      <div className="divide-y divide-line overflow-hidden rounded-card border border-line bg-surface">
        {CESTA_PITANJA.map((p) => (
          <details key={p.pitanje} className="group">
            <summary className="flex cursor-pointer list-none items-center justify-between gap-4 px-5 py-4 text-sm font-semibold text-ink [&::-webkit-details-marker]:hidden">
              {p.pitanje}
              <ChevronDown className="h-4 w-4 shrink-0 text-muted transition group-open:rotate-180" />
            </summary>
            <p className="px-5 pb-4 text-sm leading-relaxed text-muted">{p.odgovor(kontekst)}</p>
          </details>
        ))}
      </div>
    </>
  );
}
