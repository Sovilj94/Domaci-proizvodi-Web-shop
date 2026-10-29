"use client";

import { usePathname } from "next/navigation";
import { useDeliverySlots, useSedmica } from "@/lib/api/hooks";
import { aktivniKrug, daniDostave, daniDostaveTekst, rokTekst } from "@/lib/sedmica";

/**
 * Krem traka ispod zaglavlja (farmit: „🚚 Poruči do četvrtka u 12h i stiže ti prva gajbica u
 * petak ili subotu"). Rok i dani dolaze sa servera; dok ne stignu, traka se ne prikazuje.
 * Na mobilnom tekst putuje (marquee) — sadržaj je dupliran da kretanje bude neprekidno.
 */
export function TrakaRoka() {
  const { data: sedmica } = useSedmica();
  const { data: slotovi } = useDeliverySlots();
  // Početna nema traku — tamo je zaglavlje preko hero fotografije (kao kod farmit-a).
  const pathname = usePathname();
  if (!sedmica || pathname === "/") return null;

  const { rok, isporuka } = aktivniKrug(sedmica);
  const dani = slotovi?.length ? daniDostaveTekst(daniDostave(isporuka, slotovi)) : null;
  const tekst = `🚚 Poruči do ${rokTekst(rok)} i stiže ti korpica${dani ? ` u ${dani}` : ""}`;

  return (
    // shrink-0: layout je flex kolona, a overflow-hidden spušta min-height na 0 — bez ovoga
    // `main` (flex-1) skupi traku na pola visine i odsiječe tekst.
    <div className="shrink-0 overflow-hidden bg-beige py-2.5 text-sm text-ink">
      <p className="hidden text-center md:block">{tekst}</p>
      <div className="flex w-max animate-marquee gap-16 whitespace-nowrap md:hidden" aria-label={tekst}>
        <span aria-hidden>{tekst}</span>
        <span aria-hidden>{tekst}</span>
        <span aria-hidden>{tekst}</span>
        <span aria-hidden>{tekst}</span>
      </div>
    </div>
  );
}
