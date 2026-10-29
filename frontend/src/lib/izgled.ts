/*
 * Fotografije izgleda (R1). Jedino mjesto sa putanjama — vlasnik zamijeni fajl u `public/`
 * ili putanju ovdje. Dok ne stignu prave fotografije, stoje postojeće slike proizvoda.
 * Popis i preporučene dimenzije: `public/izgled/README.md`.
 */
export const IZGLED = {
  hero: {
    korpice: "/hero/hero-domacinstvo.jpg",
    voce: "/slike/products/jabuka-idared.jpg",
    spajz: "/slike/products/domaci-ajvar.jpg",
  },
  /** Fotografija preko cijele širine sa porukom o domaćinima. */
  domacini: "/hero/hero-domacinstvo.jpg",
  /** Fotografija korpice u sekciji „Hoću svoju korpicu!". */
  korpica: "/slike/products/paradajz-iz-plastenika.jpg",
  /** Fotografije uz utiske kupaca, redom (utisak nema svoju fotografiju). */
  utisci: [
    "/slike/products/domaci-krompir.jpg",
    "/slike/products/babura-paprika.jpg",
    "/slike/products/mrkva.jpg",
    "/slike/products/crni-luk.jpg",
    "/slike/products/jabuka-idared.jpg",
  ],
} as const;
