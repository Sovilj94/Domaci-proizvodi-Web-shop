import { HeroKarusel } from "@/components/pocetna/hero-karusel";
import { OvaSedmica } from "@/components/pocetna/ova-sedmica";
import { UtisciKarusel } from "@/components/pocetna/utisci-karusel";
import { NjivaDoStola } from "@/components/pocetna/njiva-do-stola";
import { AnalizeKarusel } from "@/components/pocetna/analize-karusel";
import { KakoFunkcionise } from "@/components/pocetna/kako-funkcionise";
import { DomaciniBaner } from "@/components/pocetna/domacini-baner";
import { DomaciniKarusel } from "@/components/pocetna/domacini-karusel";
import { HocuKorpicu } from "@/components/pocetna/hocu-korpicu";
import { CestaPitanja } from "@/components/pocetna/cesta-pitanja";
import { CESTA_PITANJA, popuniOdgovor } from "@/lib/cesta-pitanja";
import { stranica } from "@/lib/seo/metadata";
import { seoApi } from "@/lib/seo/api";
import { JsonLd, pitanjaLd } from "@/lib/seo/json-ld";
import { SITE_DESCRIPTION } from "@/lib/seo/site";

export const metadata = {
  ...stranica({ naslov: "E piljara — domaće sa Manjače, na tvoja vrata", opis: SITE_DESCRIPTION, putanja: "/" }),
  // Početna ne nosi šablon „· E piljara" — ime je već u naslovu.
  title: { absolute: "E piljara — domaće sa Manjače, na tvoja vrata" },
};

/** Česta pitanja za FAQPage JSON-LD se osvježavaju kao i ostali SEO podaci (lib/seo/api). */
export const revalidate = 600;

/**
 * Početna po uzoru na farmit (R1) — deset sekcija istim redom kao uzor
 * (docs/superpowers/farmit-snimak-2026-10-16.md). Sekcije koje zavise od objavljenih
 * podataka (utisci, analize, domaćini) se ne prikazuju dok podataka nema.
 *
 * Serverska komponenta (zbog SEO-a: metadata + JSON-LD); sekcije koje trebaju podatke su
 * same klijentske.
 */
export default async function HomePage() {
  // Isti izvor kao harmonika: pitanja iz admin panela, a dok ih nema — ugrađena.
  // Bez brojeva iz sedmice (rok, popust), pa odgovor ostaje tačan i kad se rok promijeni.
  const sadrzaj = await seoApi.sadrzajSajta();
  const pitanja = sadrzaj?.pitanja?.length
    ? sadrzaj.pitanja.map((p) => ({ pitanje: p.pitanje, odgovor: popuniOdgovor(p.odgovor, {}) }))
    : CESTA_PITANJA.map((p) => ({ pitanje: p.pitanje, odgovor: p.odgovor({}) }));

  return (
    <div>
      <JsonLd data={pitanjaLd(pitanja)} />
      <HeroKarusel />
      <OvaSedmica />
      <UtisciKarusel />
      <NjivaDoStola />
      <AnalizeKarusel />
      <KakoFunkcionise />
      <DomaciniBaner />
      <DomaciniKarusel />
      <HocuKorpicu />
      <CestaPitanja />
    </div>
  );
}
