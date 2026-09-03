"use client";

import Link from "next/link";
import {
  ArrowRight,
  Leaf,
  MapPin,
  MessagesSquare,
  PackageCheck,
  Sprout,
  Truck,
} from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { ProductCard } from "@/components/shop/product-card";
import { ProductGridSkeleton } from "@/components/shop/product-grid";
import { buttonVariants } from "@/components/ui/button";
import { Reveal } from "@/components/ui/reveal";
import { useCategories, useProducers, useProducts } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

/**
 * Uređivački raspored: pune sekcije naizmjenično bijela / pijesak, ujednačen vertikalni
 * ritam, mreža umjesto kartica u kutijama.
 *
 * Horizontalne trake su namjerno zadržane ispod `sm`. Telefon ima donju tab traku i korpu
 * — mreža od četiri u redu tamo znači beskonačno skrolanje do prvog proizvoda, pa raspored
 * predloška (koji je brošura, ne prodavnica) vrijedi tek od tableta naviše.
 */

/** Zajednički okvir sekcije: puna širina, unutrašnja kolona, isti ritam svuda. */
function Section({
  children,
  alt = false,
  className,
  id,
}: {
  children: React.ReactNode;
  alt?: boolean;
  className?: string;
  id?: string;
}) {
  return (
    <section id={id} className={cn("px-4 py-14 sm:py-20", alt && "bg-sand", className)}>
      <div className="mx-auto max-w-6xl">{children}</div>
    </section>
  );
}

function SectionHeader({
  naslov,
  opis,
  link,
  linkTekst,
}: {
  naslov: string;
  opis?: string;
  link?: string;
  linkTekst?: string;
}) {
  return (
    <div className="mb-8 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 className="font-display text-3xl font-extrabold text-ink sm:text-4xl">{naslov}</h2>
        {opis && <p className="mt-2 max-w-xl text-muted">{opis}</p>}
      </div>
      {link && (
        <Link href={link} className="text-sm font-semibold text-brand-700 hover:underline">
          {linkTekst} →
        </Link>
      )}
    </div>
  );
}

export default function HomePage() {
  const categories = useCategories();
  const featured = useProducts({ featured: true });
  const producers = useProducers();

  return (
    <div>
      {/* ---------- Hero ---------- */}
      <section className="relative overflow-hidden bg-gradient-to-b from-brand-50 via-sand to-cream px-4 py-20 text-center sm:py-28">
        <div className="mx-auto max-w-3xl">
          <span className="inline-flex items-center gap-2 rounded-full bg-brand-100 px-4 py-1.5 text-xs font-bold uppercase tracking-widest text-brand-700">
            <Leaf className="h-3.5 w-3.5" /> Direktno od domaćina
          </span>
          {/* clamp: 44px na telefonu, do 80px na širokom ekranu — bez skokova po breakpointima. */}
          <h1 className="mt-6 font-display font-extrabold leading-[1.05] text-ink [font-size:clamp(2.75rem,7vw,5rem)]">
            Domaće na tvoja vrata.
          </h1>
          <p className="mx-auto mt-6 max-w-xl text-lg text-muted">
            Povrće, sir, jaja, med i zimnica od domaćina sa Manjače. Bez pijace, bez
            posrednika — dostava u Banju Luku.
          </p>
          <div className="mt-9 flex flex-wrap justify-center gap-3">
            <Link href="/products" className={buttonVariants({ size: "lg" })}>
              Kupuj proizvode <ArrowRight className="h-4 w-4" />
            </Link>
            <Link
              href="#domacini"
              className={buttonVariants({ variant: "secondary", size: "lg" })}
            >
              Naši domaćini
            </Link>
          </div>
        </div>
      </section>

      {/* ---------- Kategorije ---------- */}
      <Section alt>
        <SectionHeader
          naslov="Naše kategorije"
          opis="Izaberi kategoriju i nađi sve za domaću trpezu."
          link="/products"
          linkTekst="Svi proizvodi"
        />
        {categories.isLoading ? (
          <div className="grid gap-6 [grid-template-columns:repeat(auto-fit,minmax(240px,1fr))]">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-80 animate-pulse rounded-card bg-white" />
            ))}
          </div>
        ) : (
          /* auto-fit umjesto fiksne četiri kolone: zadnji red se razvuče preko slobodne
             širine umjesto da zadnja pločica visi sama — predložak upravo to ima. */
          <div className="grid gap-6 [grid-template-columns:repeat(auto-fit,minmax(240px,1fr))]">
            {categories.data?.map((c) => (
              <Link
                key={c.id}
                href={`/products?category=${c.slug}`}
                className="group relative block h-80 overflow-hidden rounded-card bg-ink"
              >
                <SmartImage
                  src={c.imageUrl}
                  alt={c.name}
                  sizes="(max-width: 640px) 100vw, 300px"
                  className="opacity-80 transition duration-500 group-hover:scale-105"
                />
                {/* Gradijent, ne ravna prekrivka: vrh slike ostaje čitljiv, dno nosi tekst. */}
                <div className="absolute inset-0 bg-gradient-to-t from-ink via-ink/60 to-transparent" />
                <div className="absolute inset-x-0 bottom-0 p-5 text-white">
                  <h3 className="font-display text-xl font-bold">{c.name}</h3>
                  {c.productCount != null && (
                    <p className="mt-1 text-sm text-white/70">{c.productCount} proizvoda</p>
                  )}
                </div>
              </Link>
            ))}
          </div>
        )}
      </Section>

      {/* ---------- Istaknuti proizvodi ---------- */}
      <Section>
        <SectionHeader
          naslov="Istaknuto ove sedmice"
          opis="Najtraženiji artikli, uvijek svježi."
          link="/products"
          linkTekst="Svi proizvodi"
        />
        {featured.isLoading ? (
          <ProductGridSkeleton count={4} />
        ) : featured.data && featured.data.length > 0 ? (
          <>
            {/* Telefon: traka. Tablet naviše: mreža. */}
            <div className="no-scrollbar -mx-4 flex gap-4 overflow-x-auto px-4 pb-1 sm:hidden">
              {featured.data.slice(0, 10).map((p) => (
                <div key={p.id} className="w-40 shrink-0">
                  <ProductCard product={p} />
                </div>
              ))}
            </div>
            <div className="hidden gap-8 sm:grid sm:grid-cols-2 lg:grid-cols-4">
              {featured.data.slice(0, 8).map((p) => (
                <ProductCard key={p.id} product={p} />
              ))}
            </div>
          </>
        ) : (
          <p className="text-muted">Trenutno nema izdvojenih proizvoda.</p>
        )}
      </Section>

      {/* ---------- Naša priča + obećanja ---------- */}
      <Section alt id="kako">
        <div className="grid gap-12 lg:grid-cols-[1fr_1.1fr]">
          <div>
            <span className="text-xs font-bold uppercase tracking-widest text-brand-700">
              Naša priča
            </span>
            <h2 className="mt-3 font-display text-3xl font-extrabold leading-tight text-ink sm:text-4xl">
              Provjereno, domaće, s Manjače.
            </h2>
            <p className="mt-5 text-muted">
              Sarađujemo sa porodicama sa Manjače koje poznajemo lično. Bez konzervansa, bez
              industrijske obrade, bez dugih lanaca snabdijevanja — svaki proizvod dolazi od
              domaćina kojeg možemo imenovati. Narudžbe primamo do utorka, a dostavljamo
              četvrtkom.
            </p>
          </div>

          <div className="grid gap-6 sm:grid-cols-2">
            <Obecanje
              icon={<Sprout className="h-5 w-5" />}
              naslov="Provjereni domaćini"
              tekst="Sarađujemo samo sa gazdinstvima koja lično poznajemo."
            />
            <Obecanje
              icon={<Truck className="h-5 w-5" />}
              naslov="Dostava četvrtkom"
              tekst="Kombiji voze na kućni prag u Banjoj Luci, petak po potrebi."
            />
            <Obecanje
              icon={<PackageCheck className="h-5 w-5" />}
              naslov="Pakujemo na Manjači"
              tekst="Srijedom domaćini beru i pakuju — bez skladišta između."
            />
            <Obecanje
              icon={<MessagesSquare className="h-5 w-5" />}
              naslov="Podrška na telefonu"
              tekst="Dostupni smo za pitanja o porijeklu i uslovima dostave."
            />
          </div>
        </div>
      </Section>

      {/* ---------- Domaćini ---------- */}
      <Reveal>
        <Section id="domacini">
          <SectionHeader
            naslov="Naši domaćini"
            opis="Iza svakog proizvoda stoji porodica sa Manjače."
          />
          <div className="grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
            {producers.isLoading
              ? Array.from({ length: 4 }).map((_, i) => (
                  <div key={i} className="h-72 animate-pulse rounded-card bg-sand" />
                ))
              : producers.data?.map((p, i) => (
                  <Reveal key={p.id} className="h-full" delay={i * 0.06}>
                    <article className="flex h-full flex-col">
                      <div className="relative aspect-[4/3] overflow-hidden rounded-card bg-sand">
                        <SmartImage src={p.photoUrl} alt={p.name} sizes="300px" />
                      </div>
                      <h3 className="mt-3 font-display text-lg font-bold text-ink">{p.name}</h3>
                      <p className="flex items-center gap-1 text-xs font-semibold uppercase tracking-wide text-brand-700">
                        <MapPin className="h-3.5 w-3.5" /> {p.village}
                      </p>
                      <p className="mt-2 line-clamp-3 text-sm text-muted">{p.description}</p>
                    </article>
                  </Reveal>
                ))}
          </div>
        </Section>
      </Reveal>

      {/* ---------- Tamna traka pred footerom ---------- */}
      <section className="bg-ink px-4 py-16 text-center text-white sm:py-20">
        <div className="mx-auto max-w-2xl">
          <h2 className="font-display text-3xl font-extrabold leading-tight sm:text-4xl">
            Dostavljamo u tvoj kraj.
          </h2>
          <p className="mt-4 text-white/70">
            Centar, Borik, Lauš i Starčevica — narudžbe do utorka u 12h, dostava četvrtkom.
          </p>
          <div className="mt-8 flex flex-wrap justify-center gap-2">
            {["Centar", "Borik", "Lauš", "Starčevica"].map((z) => (
              <span
                key={z}
                className="rounded-full bg-white/10 px-4 py-2 text-sm font-medium text-white"
              >
                {z}
              </span>
            ))}
          </div>
          <Link
            href="/products"
            className={cn(buttonVariants({ size: "lg" }), "mt-9")}
          >
            Počni kupovinu <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      </section>
    </div>
  );
}

function Obecanje({
  icon,
  naslov,
  tekst,
}: {
  icon: React.ReactNode;
  naslov: string;
  tekst: string;
}) {
  return (
    <div>
      <div className="grid h-11 w-11 place-items-center rounded-full bg-brand-100 text-brand-700">
        {icon}
      </div>
      <h3 className="mt-4 font-display text-base font-bold text-ink">{naslov}</h3>
      <p className="mt-1.5 text-sm text-muted">{tekst}</p>
    </div>
  );
}
