/**
 * Kontakt podaci — jedini izvor. Footer i /o-nama ih čitaju odavde (spec P5, O4); do P5 su
 * stajali samo u footeru, kao tekst. Zone isporuke se NE drže ovdje: one su u bazi
 * (DeliveryZone), a footer ih piše kao kratak spisak.
 */
export const KONTAKT = {
  telefon: "+387 51 000 000",
  telefonHref: "tel:+38751000000",
  adresa: "Manjača, Banja Luka",
  podrucje: "Banja Luka i okolina",
} as const;
