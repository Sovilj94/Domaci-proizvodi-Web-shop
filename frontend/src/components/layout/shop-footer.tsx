import Link from "next/link";
import { MapPin, Phone } from "lucide-react";
import { Logo } from "@/components/layout/logo";
import { FooterRok } from "@/components/layout/footer-rok";
import { PismoPrekidac } from "@/components/pismo/pismo-prekidac";
import { KONTAKT } from "@/lib/kontakt";

/**
 * Footer je taman, kao zadnji od naizmjeničnih pojaseva stranice. Sekundarni tekst
 * ide u `muted-dark`, ne `muted` — ovaj drugi na `night` daje 2.67:1 i pada AA.
 */
export function ShopFooter() {
  return (
    <footer
      className="bg-night text-cream pb-[calc(5rem+env(safe-area-inset-bottom))] md:pb-0"
    >
      {/*
       * Rezervacija za fiksnu donju traku (56px) ide ovdje, ne na <main>: traka je
       * fiksirana na dno VIEWPORT-a, pa pokriva dno onoga što je zadnje u toku — a to
       * je footer, poslije <main>. Na telefonima sa zarezom raste za safe-area inset.
       */}
      {/* Brend stupac je širi od ostalih — nosi opis, a ne spisak linkova. */}
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-16 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1fr_1fr_1fr]">
        <div className="space-y-4">
          <Logo tone="dark" />
          <p className="max-w-xs text-sm leading-relaxed text-muted-dark">
            Domaći proizvodi sa Manjače, spakovani i dostavljeni na tvoja vrata u Banjoj
            Luci.
          </p>
        </div>

        <div>
          <h3 className="mb-4 text-xs font-bold uppercase tracking-[0.18em] text-honey-500">
            Kupovina
          </h3>
          <ul className="space-y-2.5 text-sm text-muted-dark">
            <li>
              <Link href="/products" className="transition hover:text-cream">
                Svi proizvodi
              </Link>
            </li>
            <li>
              <Link href="/cart" className="transition hover:text-cream">
                Korpa
              </Link>
            </li>
            <li>
              <Link href="/moj-nalog/narudzbe" className="transition hover:text-cream">
                Moje narudžbe
              </Link>
            </li>
          </ul>
        </div>

        <div>
          <h3 className="mb-4 text-xs font-bold uppercase tracking-[0.18em] text-honey-500">
            E piljara
          </h3>
          <ul className="space-y-2.5 text-sm text-muted-dark">
            <li>
              <Link href="/o-nama" className="transition hover:text-cream">
                O nama
              </Link>
            </li>
            <li>
              <Link href="/proizvodjaci" className="transition hover:text-cream">
                Naši domaćini
              </Link>
            </li>
            <li>
              <Link href="/recepti" className="transition hover:text-cream">
                Recepti
              </Link>
            </li>
            <li>
              <Link href="/analize" className="transition hover:text-cream">
                Analize
              </Link>
            </li>
            <li>
              <Link href="/postani-partner" className="transition hover:text-cream">
                Postani partner
              </Link>
            </li>
          </ul>
        </div>

        <div id="dostava">
          <h3 className="mb-4 text-xs font-bold uppercase tracking-[0.18em] text-honey-500">
            Dostava
          </h3>
          <ul className="space-y-2.5 text-sm text-muted-dark">
            <FooterRok />
            <li className="flex items-center gap-2">
              <MapPin className="h-4 w-4 shrink-0 text-sage" /> Centar · Borik · Lauš ·
              Starčevica
            </li>
          </ul>
        </div>

        <div>
          <h3 className="mb-4 text-xs font-bold uppercase tracking-[0.18em] text-honey-500">
            Kontakt
          </h3>
          <ul className="space-y-2.5 text-sm text-muted-dark">
            <li className="flex items-center gap-2">
              <Phone className="h-4 w-4 shrink-0 text-sage" />
              <a href={KONTAKT.telefonHref} translate="no" className="transition hover:text-cream">
                {KONTAKT.telefon}
              </a>
            </li>
            <li className="flex items-center gap-2">
              <MapPin className="h-4 w-4 shrink-0 text-sage" /> {KONTAKT.adresa}
            </li>
          </ul>
        </div>
      </div>

      <div className="border-t border-white/10 py-6">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 text-xs text-muted-dark">
          <p>© {new Date().getFullYear()} E piljara. Svježe sa Manjače.</p>
          <PismoPrekidac className="font-medium hover:text-cream" />
        </div>
      </div>
    </footer>
  );
}
