import Link from "next/link";
import { MapPin, Phone, Truck } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { KONTAKT } from "@/lib/kontakt";

export const metadata = { title: "O nama" };

export default function ONamaPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-8 sm:py-12">
      <h1 className="font-display text-3xl font-bold text-ink sm:text-4xl">O nama</h1>
      <p className="mt-3 text-lg text-muted">
        E piljara spaja domaćinstva sa Manjače sa kupcima u Banjoj Luci i okolini. Prodajemo
        ono što bismo i sami jeli.
      </p>

      <section className="mt-10">
        <h2 className="font-display text-2xl font-bold text-ink">Kako radimo</h2>
        <div className="mt-4 space-y-4 text-muted">
          <p>
            Radimo u sedmičnom ciklusu. Sadržaj korpica sastavlja se svake sedmice, narudžbe
            se primaju do roka koji piše na stranici korpica, a isporuku radimo jednom
            sedmično.
          </p>
          <p>
            Plaća se pouzećem, pri dostavi. Ništa ne plaćaš unaprijed.
          </p>
          <p>
            Domaćini od kojih dolazi roba navedeni su poimence na stranici{" "}
            <Link href="/proizvodjaci" className="font-medium text-brand-700 underline">
              Naši domaćini
            </Link>
            . Nalaze laboratorijskih analiza objavljujemo na stranici{" "}
            <Link href="/analize" className="font-medium text-brand-700 underline">
              Analize
            </Link>
            , javno, da ih može pogledati svako.
          </p>
        </div>
      </section>

      <section className="mt-10">
        <h2 className="font-display text-2xl font-bold text-ink">Postani partner</h2>
        <p className="mt-4 text-muted">
          Imate gazdinstvo i želite da vaši proizvodi stignu do kupaca u gradu? Javite nam se
          preko{" "}
          <Link href="/postani-partner" className="font-medium text-brand-700 underline">
            prijave za partnere
          </Link>
          .
        </p>
      </section>

      <section id="kontakt" className="mt-10 scroll-mt-24">
        <h2 className="font-display text-2xl font-bold text-ink">Kontakt</h2>
        <ul className="mt-4 space-y-3 text-muted">
          <li className="flex items-center gap-2">
            <Phone className="h-4 w-4 shrink-0 text-brand-700" aria-hidden />
            <a href={KONTAKT.telefonHref} className="font-medium text-ink hover:underline">
              {KONTAKT.telefon}
            </a>
          </li>
          <li className="flex items-center gap-2">
            <MapPin className="h-4 w-4 shrink-0 text-brand-700" aria-hidden />
            {KONTAKT.adresa}
          </li>
          <li className="flex items-center gap-2">
            <Truck className="h-4 w-4 shrink-0 text-brand-700" aria-hidden />
            Dostavljamo: {KONTAKT.podrucje}
          </li>
        </ul>
      </section>

      <div className="mt-10 flex flex-wrap gap-3">
        <Link href="/korpice" className={buttonVariants({ variant: "primary", size: "lg" })}>
          Izaberi korpicu
        </Link>
        <Link href="/proizvodjaci" className={buttonVariants({ variant: "secondary", size: "lg" })}>
          Naši domaćini
        </Link>
      </div>
    </div>
  );
}
