"use client";

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

/**
 * Početna po uzoru na farmit (R1) — deset sekcija istim redom kao uzor
 * (docs/superpowers/farmit-snimak-2026-10-16.md). Sekcije koje zavise od objavljenih
 * podataka (utisci, analize, domaćini) se ne prikazuju dok podataka nema.
 */
export default function HomePage() {
  return (
    <div>
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
