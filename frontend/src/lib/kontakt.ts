/**
 * Kontakt podaci — jedini izvor je baza (`SadrzajSajta`, admin `/admin/sadrzaj`). Footer, /o-nama
 * i JSON-LD ih čitaju odavde. Polje koje admin nije unio je null i ne prikazuje se: sajt nema
 * svoj ugrađeni kontakt. Zone isporuke se NE drže ovdje: one su u bazi (DeliveryZone).
 */
import type { SadrzajSajta } from "@/types/domain";

/** Kontakt kakav ga je admin unio. Link za poziv se gradi iz broja: cifre i vodeći „+". */
export function kontaktIz(s?: SadrzajSajta | null) {
  const telefon = s?.telefon || null;
  return {
    telefon,
    telefonHref: telefon ? "tel:" + telefon.replace(/[^\d+]/g, "") : null,
    email: s?.email || null,
    adresa: s?.adresa || null,
    podrucje: s?.podrucje || null,
  };
}
