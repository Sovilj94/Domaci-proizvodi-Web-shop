"use client";

import { Check } from "lucide-react";
import { useBlokovi } from "@/lib/api/hooks";

/** Šta piljara nudi domaćinu — stavke su u bazi (sekcija „partner-ponuda"). */
export function PartnerPonuda() {
  const { blokovi } = useBlokovi("partner-ponuda");
  if (blokovi.length === 0) return null;
  return (
    <ul className="mt-6 space-y-3">
      {blokovi.map((b) => (
        <li key={b.id} className="flex items-start gap-3 text-ink">
          <Check className="mt-0.5 h-5 w-5 shrink-0 text-brand-700" aria-hidden />
          {b.naslov}
        </li>
      ))}
    </ul>
  );
}
