"use client";

import { Truck } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { korpiceApi } from "@/lib/api/endpoints";

// Dva padeža za dane u sedmici — "u" traži akuzativ, "do" traži genitiv, i nisu isti
// oblik (npr. "petak" → "petka", ne "petaku"). Indeks je JS Date.getDay() (0 = nedjelja).
const DANI_AKUZATIV = ["nedjelju", "ponedjeljak", "utorak", "srijedu", "četvrtak", "petak", "subotu"];
const DANI_GENITIV = ["nedjelje", "ponedjeljka", "utorka", "srijede", "četvrtka", "petka", "subote"];

/**
 * Traka sa rokom za naručivanje, iznad naslova stranice. Sedmica se generiše i
 * zaključava po fiksnom raspredu na backendu (isporuka je uvijek petkom) — ovo je
 * čisto informativno, korisnik ovdje ništa ne mijenja.
 */
export function RokTraka() {
  const { data } = useQuery({
    queryKey: ["korpice", "sedmica"],
    queryFn: korpiceApi.sedmica,
    staleTime: 5 * 60 * 1000,
  });

  if (!data) return null;

  const rok = new Date(data.rokNarucivanja);
  const isporuka = new Date(data.datumIsporuke);

  return (
    <div className="w-full bg-honey-100 py-2.5 text-center text-sm text-ink/80">
      <span className="inline-flex items-center gap-2">
        <Truck className="h-4 w-4 shrink-0 text-honey-600" />
        {data.rokIstekao ? (
          "Naručivanje za ovu sedmicu je zatvoreno. Sljedeća isporuka je u petak."
        ) : (
          <>
            Poruči do {DANI_GENITIV[rok.getDay()]} u {rok.getHours()}h i korpica stiže u{" "}
            {DANI_AKUZATIV[isporuka.getDay()]} {isporuka.getDate()}.{isporuka.getMonth() + 1}.
          </>
        )}
      </span>
    </div>
  );
}
