/**
 * Kontakt podaci — jedini izvor. Footer i /o-nama ih čitaju odavde (spec P5, O4); do P5 su
 * stajali samo u footeru, kao tekst. Zone isporuke se NE drže ovdje: one su u bazi
 * (DeliveryZone), a footer ih piše kao kratak spisak.
 */
import type { SadrzajSajta } from "@/types/domain";

export const KONTAKT = {
  telefon: "+387 51 000 000",
  telefonHref: "tel:+38751000000",
  adresa: "Manjača, Banja Luka",
  podrucje: "Banja Luka i okolina",
} as const;

/**
 * Kontakt kakav ga je admin unio (`/admin/sadrzaj`), a za polje koje nije unio — ugrađena
 * vrijednost iznad. Link za poziv se gradi iz broja: cifre i vodeći „+".
 */
export function kontaktIz(s?: SadrzajSajta | null) {
  const telefon = s?.telefon || KONTAKT.telefon;
  return {
    telefon,
    telefonHref: s?.telefon ? "tel:" + telefon.replace(/[^\d+]/g, "") : KONTAKT.telefonHref,
    email: s?.email || null,
    adresa: s?.adresa || KONTAKT.adresa,
    podrucje: s?.podrucje || KONTAKT.podrucje,
  };
}
