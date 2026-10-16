/*
 * Fotografije izgleda (R1). Jedino mjesto sa putanjama — vlasnik zamijeni fajl u `public/`
 * ili putanju ovdje. Dok ne stignu prave fotografije, stoje postojeće slike proizvoda.
 * Popis i preporučene dimenzije: `public/izgled/README.md`.
 */
export const IZGLED = {
  hero: {
    korpice: "/hero/hero-domacinstvo.jpg",
    voce: "/products/jabuka-idared.jpg",
    spajz: "/products/domaci-ajvar.jpg",
  },
  /** Fotografija preko cijele širine sa porukom o domaćinima. */
  domacini: "/hero/hero-domacinstvo.jpg",
  /** Fotografija korpice u sekciji „Hoću svoju korpicu!". */
  korpica: "/products/paradajz-iz-plastenika.jpg",
  /** Fotografije uz utiske kupaca, redom (utisak nema svoju fotografiju). */
  utisci: [
    "/products/domaci-krompir.jpg",
    "/products/babura-paprika.jpg",
    "/products/mrkva.jpg",
    "/products/crni-luk.jpg",
    "/products/jabuka-idared.jpg",
  ],
} as const;
