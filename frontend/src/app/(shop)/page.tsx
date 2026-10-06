"use client";

import Link from "next/link";
import Image from "next/image";
import { ArrowRight } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { ProductCard } from "@/components/shop/product-card";
import { ProductGridSkeleton } from "@/components/shop/product-grid";
import { BenefitCards } from "@/components/shop/benefit-cards";
import { CategoryTile } from "@/components/shop/category-tile";
import { SectionHeader } from "@/components/ui/section-header";
import { buttonVariants } from "@/components/ui/button";
import { useCategories, useProducers, useProducts } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

/**
 * Početna po mockupu „Opcija 3": svijetla cijelom dužinom. Stari izgled je imao dvije
 * tamne sekcije (hero i izdvojeni proizvodi) — one su uklonjene, jer tamnu površinu na
 * mockupu nosi samo donji promo baner.
 */

/** Zajednički okvir sekcije: puna širina, unutrašnja kolona, isti ritam svuda. */
function Section({
  children,
  className,
  id,
}: {
  children: React.ReactNode;
  className?: string;
  id?: string;
}) {
  return (
    <section id={id} className={cn("px-4 py-8 sm:py-12", className)}>
      <div className="mx-auto max-w-6xl">{children}</div>
    </section>
  );
}

export default function HomePage() {
  const categories = useCategories();
  const featured = useProducts({ featured: true });
  const producers = useProducers();

  return (
    <div>
      {/* ---------- Hero ---------- */}
      <Section className="pt-6">
        <div className="relative overflow-hidden rounded-card border border-line bg-surface">
          <div className="grid items-center gap-6 md:grid-cols-2">
            <div className="order-2 p-6 sm:p-10 md:order-1">
              <span className="text-xs font-bold uppercase tracking-[0.18em] text-brand-600">
                Zdrava ishrana
              </span>
              <h1 className="mt-3 font-display text-3xl font-bold leading-[1.1] text-ink sm:text-5xl">
                Bolji izbori
                <br />
                za zdraviji život.
              </h1>
              <p className="mt-4 max-w-md text-sm text-muted sm:text-base">
                Svježe, domaće i kvalitetne namirnice na jednom mjestu.
              </p>
              <Link
                href="/products"
                className={cn(buttonVariants({ size: "lg" }), "mt-6")}
              >
                Pogledaj ponudu
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
            <div className="relative order-1 h-56 md:order-2 md:h-[380px]">
              <Image
                src="/hero/hero-domacinstvo.jpg"
                alt="Svježe povrće sa Manjače"
                fill
                priority
                sizes="(max-width: 768px) 100vw, 50vw"
                className="object-cover"
              />
            </div>
          </div>
        </div>
      </Section>

      {/* ---------- Benefiti ---------- */}
      <Section className="py-2">
        <BenefitCards />
      </Section>

      {/* ---------- Popularne kategorije ---------- */}
      <Section>
        <SectionHeader naslov="Popularne kategorije" link="/kategorije" />
        {categories.isLoading ? (
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i}>
                <div className="skeleton aspect-square rounded-card" />
                <div className="skeleton mx-auto mt-2 h-3 w-14 rounded" />
              </div>
            ))}
          </div>
        ) : (
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {categories.data?.slice(0, 6).map((c) => (
              <CategoryTile key={c.id} category={c} />
            ))}
          </div>
        )}
      </Section>

      {/* ---------- Izdvojeni proizvodi ---------- */}
      <Section>
        <SectionHeader naslov="Izdvojeni proizvodi" link="/products" />
        {featured.isLoading ? (
          <ProductGridSkeleton />
        ) : (
          /* Iste kolone i razmak kao MREZA u product-grid.tsx — inače skeleton i
             učitani sadržaj imaju različit raspored i stranica skoči. */
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4 lg:gap-4">
            {featured.data?.slice(0, 8).map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        )}
      </Section>

      {/* ---------- Domaćini ---------- */}
      <Section id="domacini">
        <SectionHeader
          naslov="Naši domaćini"
          opis="Ljudi koji stoje iza svakog proizvoda."
        />
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:gap-4">
          {producers.data?.slice(0, 4).map((p) => (
            <div
              key={p.id}
              className="overflow-hidden rounded-card border border-line bg-surface"
            >
              <div className="relative aspect-[4/3] bg-sand">
                <SmartImage src={p.photoUrl} alt={p.name} sizes="(max-width: 640px) 50vw, 25vw" />
              </div>
              <div className="p-3.5">
                <p className="text-sm font-semibold text-ink">{p.name}</p>
                <p className="mt-0.5 text-xs text-muted">{p.village}</p>
                <p className="mt-1.5 line-clamp-2 text-xs text-muted">{p.description}</p>
              </div>
            </div>
          ))}
        </div>
      </Section>

      {/* ---------- Promo baner ---------- */}
      <Section className="pb-14">
        <div className="relative overflow-hidden rounded-card">
          <Image
            src="/hero/hero-domacinstvo.jpg"
            alt=""
            fill
            sizes="100vw"
            className="object-cover"
          />
          {/*
            Jedna od dvije preostale tamne površine u aplikaciji (druga je footer).
            Prekrivka je obavezna: bijeli tekst direktno na fotografiji pada ispod AA.
          */}
          <div className="absolute inset-0 bg-brand-700/85" />
          <div className="relative px-6 py-10 sm:px-12 sm:py-14">
            <h2 className="font-display text-2xl font-bold text-white sm:text-4xl">
              Domaće. Svježe. Pouzdano.
            </h2>
            <p className="mt-2 max-w-md text-sm text-white/85">
              Direktno od provjerenih proizvođača sa Manjače.
            </p>
            <Link
              href="/products"
              className="mt-5 inline-flex h-11 items-center justify-center rounded-[10px] bg-white px-6 text-sm font-medium text-brand-700 transition hover:bg-cream"
            >
              Saznaj više
            </Link>
          </div>
        </div>
      </Section>
    </div>
  );
}
