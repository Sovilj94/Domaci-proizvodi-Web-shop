import type { Metadata } from "next";
import { absoluteUrl, OG_IMAGE, SITE_NAME, skratiOpis } from "./site";

/**
 * Metadata javne stranice: naslov, opis, kanonski URL i Open Graph / X kartica.
 * Naslov dobija „· E piljara" iz šablona u korijenskom layoutu.
 */
export function stranica({
  naslov,
  opis,
  putanja,
  slika,
  tip = "website",
}: {
  naslov: string;
  opis?: string | null;
  putanja: string;
  slika?: string | null;
  tip?: "website" | "article";
}): Metadata {
  const description = skratiOpis(opis);
  const images = [slika ? { url: absoluteUrl(slika), alt: naslov } : OG_IMAGE];
  return {
    // `absolute`: šablon iz korijena ne stiže do stranice ispod layouta koji ima svoj naslov
    // kao običan string (products, recepti…), pa sufiks pišemo sami — isti na svakoj stranici.
    title: { absolute: `${naslov} · ${SITE_NAME}` },
    description,
    alternates: { canonical: putanja },
    // openGraph u podstranici ZAMJENJUJE roditeljski (ne spaja se), pa siteName/locale idu opet.
    openGraph: { title: naslov, description, url: putanja, type: tip, images, siteName: SITE_NAME, locale: "sr_BA" },
    twitter: { card: "summary_large_image", title: naslov, description, images: images.map((i) => i.url) },
  };
}

/** Entitet koji API sigurno nema: layout vraća 404, a ovo sprječava „index" i kanonski URL na njoj. */
export const nijePronadjeno: Metadata = {
  title: "Stranica nije pronađena",
  robots: { index: false, follow: true },
  // Bez ovoga 404 proizvoda naslijedi kanonski URL sekcije (/spajz).
  alternates: { canonical: null },
  openGraph: null,
};

/**
 * Privatne i transakcione stranice (nalog, korpa, checkout, admin, linkovi sa tokenom):
 * ne indeksirati i ne pratiti linkove. Tokeni u URL-u su lozinke (vidi PRODUCTION.md).
 */
export function privatno(naslov: Metadata["title"]): Metadata {
  return { title: naslov, robots: { index: false, follow: false, googleBot: { index: false, follow: false } } };
}
