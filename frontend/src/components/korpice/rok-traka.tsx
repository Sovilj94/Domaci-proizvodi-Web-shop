"use client";

import { Truck } from "lucide-react";
import { useSedmica } from "@/lib/api/hooks";
import { DANI_AKUZATIV, isporukaTekst, rokTekst } from "@/lib/sedmica";

/**
 * Traka sa rokom za naručivanje, iznad naslova stranice. Dan i sat isporuke dolaze iz
 * `DeliverySlot`-a na backendu (vidi `SedmicaHelper.UcitajRokAsync`), ne iz zakucanog
 * dana — zato se i "zatvoreno" poruka i sat u "Poruči do" oba čitaju iz `data`,
 * ni jedno nije literal. Prije ovog popravka je "u petak" bio hardkod koji se nije
 * slagao sa stvarnim danom (četvrtak kod seed-ovanih slotova), i sat je dolazio iz
 * ponoćnog `datumIsporuke` (backend ga sad šalje sa stvarnim vremenom slota).
 */
export function RokTraka() {
  const { data } = useSedmica();

  if (!data) return null;

  const rok = new Date(data.rokNarucivanja);
  const isporuka = new Date(data.datumIsporuke);

  return (
    <div className="w-full bg-honey-100 py-2.5 text-center text-sm text-ink/80">
      <span className="inline-flex items-center gap-2">
        <Truck className="h-4 w-4 shrink-0 text-honey-600" />
        {data.rokIstekao ? (
          <>Naručivanje za ovu sedmicu je zatvoreno. Sljedeća isporuka je u {DANI_AKUZATIV[isporuka.getDay()]}.</>
        ) : (
          <>
            Poruči do {rokTekst(rok)} i korpica stiže u {isporukaTekst(isporuka)}
          </>
        )}
      </span>
    </div>
  );
}
