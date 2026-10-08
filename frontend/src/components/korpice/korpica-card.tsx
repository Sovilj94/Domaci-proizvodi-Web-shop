"use client";

import Link from "next/link";
import { Scale, Sparkles, Sprout, Truck } from "lucide-react";
import type { Korpica } from "@/types/domain";
import { SmartImage } from "@/components/ui/smart-image";
import { Button } from "@/components/ui/button";
import { cn, formatPrice } from "@/lib/utils";

const TEZINA: Record<Korpica["velicina"], string> = {
  mala: "oko 3–5 kg",
  srednja: "oko 6–8 kg",
  velika: "oko 8–10 kg",
};

/**
 * Jedna korpica na /korpice. `naziv` i `opis` već dolaze sa veličinom i brojem
 * članova upisanim u tekst (npr. "Srednja korpica povrća" / "...Za 2 do 4 člana."),
 * zato kartica ne dupira taj podatak u zasebnoj oznaci.
 *
 * `onDodaj` dodaje korpicu u postojeću korpu — vidi `korpicaKaoProizvod` u
 * `lib/korpice.ts` za mapiranje na oblik koji korpa očekuje.
 */
export function KorpicaCard({
  korpica,
  istaknuta,
  rokIstekao,
  onPogledajSadrzaj,
  onDodaj,
}: {
  korpica: Korpica;
  istaknuta?: boolean;
  /**
   * Rok naručivanja za ovu sedmicu je prošao — sadržaj je zaključan za pakovanje, pa
   * korpica ne smije stići u korpu iako je formalno još "dostupna" (vidi `Dostupna`
   * na backendu: ona ne znaje ništa o roku, samo o zalihi). Bez ovoga kupac može
   * naručiti korpicu koja neće biti u zamrznutoj pakerskoj listi.
   */
  rokIstekao?: boolean;
  onPogledajSadrzaj: (slug: string) => void;
  onDodaj: (korpica: Korpica) => void;
}) {
  const nedostupna = !korpica.dostupna || !!rokIstekao;
  return (
    <article
      className={cn(
        "relative flex flex-col overflow-hidden rounded-card bg-surface transition hover:shadow-md",
        istaknuta ? "border-2 border-brand-600" : "border border-line",
      )}
    >
      {istaknuta && (
        <span className="absolute right-3 top-3 z-10 rounded-full bg-brand-600 px-3 py-1 text-xs font-semibold text-white">
          Najčešći izbor
        </span>
      )}

      <Link href={`/korpice/${korpica.slug}`} className="relative block aspect-[4/3] bg-sand">
        <SmartImage
          src={korpica.imageUrl}
          alt={korpica.naziv}
          sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"
        />
      </Link>

      <div className="flex flex-1 flex-col gap-3 p-4 sm:p-5">
        <div>
          <Link href={`/korpice/${korpica.slug}`}>
            <h3 className="text-base font-semibold text-ink transition hover:text-brand-700 sm:text-lg">
              {korpica.naziv}
            </h3>
          </Link>
          <p className="mt-1 text-sm text-muted">{korpica.opis}</p>
        </div>

        <ul className="flex flex-col gap-1.5 text-sm text-ink">
          <li className="flex items-center gap-2">
            <Truck className="h-4 w-4 shrink-0 text-brand-600" />
            1 dostava do tvojih vrata
          </li>
          <li className="flex items-center gap-2">
            <Scale className="h-4 w-4 shrink-0 text-brand-600" />
            {TEZINA[korpica.velicina]}
          </li>
          <li className="flex items-center gap-2">
            <Sprout className="h-4 w-4 shrink-0 text-brand-600" />
            Domaće sa Manjače, bez posrednika
          </li>
          <li className="flex items-center gap-2">
            <Sparkles className="h-4 w-4 shrink-0 text-brand-600" />
            Sadržaj se mijenja svake sedmice
          </li>
        </ul>

        <p className="mt-auto text-2xl font-bold text-ink">
          {formatPrice(korpica.cijena)}
          <span className="ml-1 text-sm font-normal text-muted">/korpica</span>
        </p>

        <Button
          type="button"
          size="lg"
          variant={istaknuta ? "primary" : "secondary"}
          className="w-full"
          onClick={() => onDodaj(korpica)}
          disabled={nedostupna}
        >
          {!korpica.dostupna
            ? "Rasprodato ove sedmice"
            : rokIstekao
              ? "Naručivanje zatvoreno za ovu sedmicu"
              : "Naruči korpicu"}
        </Button>

        <button
          type="button"
          onClick={() => onPogledajSadrzaj(korpica.slug)}
          className="text-sm font-medium text-brand-700 underline-offset-4 hover:underline"
        >
          Pogledaj sadržaj korpice
        </button>
      </div>
    </article>
  );
}
