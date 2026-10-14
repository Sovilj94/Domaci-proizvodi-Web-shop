"use client";

import { Quote } from "lucide-react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { useUtisci } from "@/lib/api/hooks";

/**
 * Utisci kupaca. Ne renderuje ništa — ni okvir sekcije — dok nema objavljenih, i kad
 * zahtjev ne uspije: prazna sekcija „Utisci" je gora od nijedne (spec, O2).
 */
export function Utisci() {
  const { data } = useUtisci();
  if (!data?.length) return null;

  return (
    <Sekcija>
      <SectionHeader naslov="Šta kažu kupci" />
      <ul className="grid gap-3 md:grid-cols-2 lg:grid-cols-3 lg:gap-4">
        {data.slice(0, 6).map((u) => (
          <li key={u.id}>
            <figure className="flex h-full flex-col rounded-card border border-line bg-surface p-5">
              <Quote className="h-5 w-5 text-brand-600" aria-hidden />
              <blockquote className="mt-3 flex-1 text-sm leading-relaxed text-ink">{u.tekst}</blockquote>
              <figcaption className="mt-4 text-sm">
                <span className="font-semibold text-ink">{u.ime}</span>
                {/* Separator samo kad mjesto postoji — inače „Marija, " visi. */}
                {u.mjesto && <span className="text-muted"> · {u.mjesto}</span>}
              </figcaption>
            </figure>
          </li>
        ))}
      </ul>
    </Sekcija>
  );
}
