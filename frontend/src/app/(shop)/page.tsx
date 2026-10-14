"use client";

import Link from "next/link";
import Image from "next/image";
import { ProductCard } from "@/components/shop/product-card";
import { ProductGridSkeleton } from "@/components/shop/product-grid";
import { BenefitCards } from "@/components/shop/benefit-cards";
import { CategoryTile } from "@/components/shop/category-tile";
import { SectionHeader } from "@/components/ui/section-header";
import { Sekcija } from "@/components/pocetna/sekcija";
import { HeroKorpica } from "@/components/pocetna/hero-korpica";
import { KakoFunkcionise } from "@/components/pocetna/kako-funkcionise";
import { Domacini } from "@/components/pocetna/domacini";
import { Utisci } from "@/components/pocetna/utisci";
import { CestaPitanja } from "@/components/pocetna/cesta-pitanja";
import { useCategories, useProducts } from "@/lib/api/hooks";

/**
 * Početna od P4 prodaje korpicu: hero sa odbrojavanjem, pa kako funkcioniše, pa izlog
 * (kategorije, izdvojeni), domaćini, utisci i česta pitanja. Svijetla cijelom dužinom kao
 * u redizajnu „Opcija 3" — tamnu površinu nosi samo donji promo baner.
 */
export default function HomePage() {
  const categories = useCategories();
  const featured = useProducts({ featured: true });

  return (
    <div>
      <Sekcija className="pt-6 sm:pt-6">
        <HeroKorpica />
      </Sekcija>

      <Sekcija className="py-2 sm:py-2">
        <BenefitCards />
      </Sekcija>

      <Sekcija>
        <KakoFunkcionise />
      </Sekcija>

      {/* ---------- Popularne kategorije ---------- */}
      <Sekcija>
        <SectionHeader naslov="Popularne kategorije" />
        {categories.isLoading ? (
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i}>
                <div className="skeleton aspect-square rounded-card" />
                <div className="skeleton mx-auto mt-2 h-3 w-14 rounded" />
              </div>
            ))}
          </div>
        ) : categories.data?.length ? (
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-4 lg:grid-cols-6">
            {categories.data?.slice(0, 6).map((c) => (
              <CategoryTile key={c.id} category={c} />
            ))}
          </div>
        ) : (
          <p className="text-muted">Kategorije trenutno nisu dostupne.</p>
        )}
      </Sekcija>

      {/* ---------- Izdvojeni proizvodi ---------- */}
      <Sekcija>
        <SectionHeader naslov="Izdvojeni proizvodi" link="/products" />
        {featured.isLoading ? (
          <ProductGridSkeleton />
        ) : featured.data?.length ? (
          /* Iste kolone i razmak kao MREZA u product-grid.tsx — inače skeleton i
             učitani sadržaj imaju različit raspored i stranica skoči. */
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4 lg:gap-4">
            {featured.data?.slice(0, 8).map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        ) : (
          <p className="text-muted">Trenutno nema izdvojenih proizvoda.</p>
        )}
      </Sekcija>

      <Sekcija id="domacini">
        <Domacini />
      </Sekcija>

      <Utisci />

      <Sekcija>
        <CestaPitanja />
      </Sekcija>

      {/* ---------- Promo baner ---------- */}
      <Sekcija className="pb-14 sm:pb-14">
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
              href="/korpice"
              className="mt-5 inline-flex h-11 items-center justify-center rounded-[10px] bg-white px-6 text-sm font-medium text-brand-700 transition hover:bg-cream"
            >
              Izaberi korpicu
            </Link>
          </div>
        </div>
      </Sekcija>
    </div>
  );
}
