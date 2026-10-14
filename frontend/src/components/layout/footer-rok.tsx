"use client";

import { Clock, Truck } from "lucide-react";
import { useSedmica } from "@/lib/api/hooks";
import { aktivniKrug, DANI_AKUZATIV, rokTekst } from "@/lib/sedmica";

/**
 * Rok i dan isporuke u footeru, iz istog izvora kao odbrojavanje na početnoj. Do P4 je
 * ovdje stajalo zakucano „Narudžbe do utorka u 12h", što se nije slagalo sa `DeliverySlot`-om.
 * Dok podatak ne stigne (ili kad API ne odgovori) ne prikazuje ništa — bolje nijedan red
 * nego pogrešan rok.
 */
export function FooterRok() {
  const { data } = useSedmica();
  if (!data) return null;

  const { rok, isporuka } = aktivniKrug(data);
  return (
    <>
      <li className="flex items-center gap-2">
        <Clock className="h-4 w-4 shrink-0 text-sage" /> Narudžbe do {rokTekst(rok)}
      </li>
      <li className="flex items-center gap-2">
        <Truck className="h-4 w-4 shrink-0 text-sage" /> Dostava u {DANI_AKUZATIV[isporuka.getDay()]}
      </li>
    </>
  );
}
