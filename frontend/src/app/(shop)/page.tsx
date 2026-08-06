"use client";

import Link from "next/link";
import { ArrowRight, CalendarClock, Leaf, MapPin, PackageCheck, Truck } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { ProductCard } from "@/components/shop/product-card";
import { ProductGridSkeleton } from "@/components/shop/product-grid";
import { buttonVariants } from "@/components/ui/button";
import { useCategories, useProducers, useProducts } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

export default function HomePage() {
  const categories = useCategories();
  const featured = useProducts({ featured: true });
  const producers = useProducers();

  return (
    <div className="pb-4">
      {/* Promo banner */}
      <section className="mx-auto max-w-6xl px-4 pt-5">
        <div className="relative overflow-hidden rounded-3xl bg-brand-700 px-6 py-8 text-white sm:px-10 sm:py-11">
          <div className="absolute inset-0 bg-[radial-gradient(60%_80%_at_90%_10%,rgba(255,255,255,0.10),transparent)]" />
          <div className="relative max-w-md">
            <span className="inline-flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold">
              <Leaf className="h-3.5 w-3.5" /> Svježe sa Manjače
            </span>
            <h1 className="mt-3 font-display text-2xl font-bold leading-tight sm:text-4xl">
              Domaće na tvoja vrata
            </h1>
            <p className="mt-2 max-w-sm text-sm text-brand-100 sm:text-base">
              Povrće, sir, jaja, med i zimnica od domaćina sa Manjače — dostava u Banju Luku.
            </p>
            <Link
              href="/products"
              className={cn(buttonVariants({ variant: "secondary", size: "lg" }), "mt-5")}
            >
              Kupuj proizvode <ArrowRight className="h-4 w-4" />
            </Link>
          </div>
        </div>
      </section>

      {/* Categories */}
      <section className="mx-auto max-w-6xl px-4 pt-8">
        <div className="mb-3 flex items-end justify-between">
          <h2 className="font-display text-lg font-bold sm:text-xl">Kategorije</h2>
          <Link href="/products" className="text-sm font-semibold text-brand-700 hover:underline">
            Sve →
          </Link>
        </div>
        {categories.isLoading ? (
          <div className="flex gap-4 overflow-hidden">
            {Array.from({ length: 6 }).map((_, i) => (
              <div key={i} className="w-24 shrink-0 sm:w-28">
                <div className="aspect-square animate-pulse rounded-2xl bg-sand" />
              </div>
            ))}
          </div>
        ) : (
          <div className="no-scrollbar -mx-4 flex gap-4 overflow-x-auto px-4 pb-1">
            {categories.data?.map((c) => (
              <Link
                key={c.id}
                href={`/products?category=${c.slug}`}
                className="group w-24 shrink-0 sm:w-28"
              >
                <div className="relative aspect-square overflow-hidden rounded-2xl bg-sand shadow-card">
                  <SmartImage
                    src={c.imageUrl}
                    alt={c.name}
                    sizes="112px"
                    className="transition duration-300 group-hover:scale-105"
                  />
                </div>
                <p className="mt-1.5 text-center text-xs font-medium leading-tight">{c.name}</p>
              </Link>
            ))}
          </div>
        )}
      </section>

      {/* Featured carousel */}
      <section className="mx-auto max-w-6xl px-4 pt-9">
        <div className="mb-3 flex items-end justify-between">
          <h2 className="font-display text-lg font-bold sm:text-xl">Izdvojeno ove sedmice</h2>
          <Link href="/products" className="text-sm font-semibold text-brand-700 hover:underline">
            Svi proizvodi →
          </Link>
        </div>
        {featured.isLoading ? (
          <ProductGridSkeleton count={4} />
        ) : featured.data && featured.data.length > 0 ? (
          <div className="no-scrollbar -mx-4 flex gap-4 overflow-x-auto px-4 pb-1">
            {featured.data.slice(0, 10).map((p) => (
              <div key={p.id} className="w-40 shrink-0 sm:w-48">
                <ProductCard product={p} />
              </div>
            ))}
          </div>
        ) : (
          <p className="text-muted">Trenutno nema izdvojenih proizvoda.</p>
        )}
      </section>

      {/* Producers */}
      <section id="domacini" className="mx-auto max-w-6xl px-4 pt-12">
        <h2 className="font-display text-lg font-bold sm:text-xl">Naši domaćini</h2>
        <p className="mt-1 max-w-xl text-sm text-muted">
          Iza svakog proizvoda stoji porodica sa Manjače koju poznajemo lično.
        </p>
        <div className="mt-5 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
          {producers.isLoading
            ? Array.from({ length: 4 }).map((_, i) => (
                <div key={i} className="h-64 animate-pulse rounded-card bg-white shadow-card" />
              ))
            : producers.data?.map((p) => (
                <article
                  key={p.id}
                  className="overflow-hidden rounded-card bg-white shadow-card"
                >
                  <div className="relative aspect-[4/3] bg-sand">
                    <SmartImage src={p.photoUrl} alt={p.name} sizes="300px" />
                  </div>
                  <div className="p-4">
                    <h3 className="font-display font-semibold">{p.name}</h3>
                    <p className="flex items-center gap-1 text-xs text-brand-700">
                      <MapPin className="h-3.5 w-3.5" /> {p.village}
                    </p>
                    <p className="mt-2 line-clamp-3 text-sm text-muted">{p.description}</p>
                  </div>
                </article>
              ))}
        </div>
      </section>

      {/* How it works */}
      <section id="kako" className="mx-auto max-w-6xl px-4 pt-12">
        <h2 className="font-display text-lg font-bold sm:text-xl">Kako funkcioniše</h2>
        <div className="mt-5 grid gap-4 md:grid-cols-3">
          <Step icon={<CalendarClock className="h-5 w-5" />} title="Naruči do utorka" text="Napuni korpu domaćim proizvodima." />
          <Step icon={<PackageCheck className="h-5 w-5" />} title="Pakujemo na Manjači" text="Srijedom domaćini beru i pakuju svježe." />
          <Step icon={<Truck className="h-5 w-5" />} title="Dostava u četvrtak" text="Kombiji dostavljaju na kućni prag u BL." />
        </div>
      </section>

      {/* Delivery strip */}
      <section className="mt-12 bg-sand">
        <div className="mx-auto flex max-w-6xl flex-col items-center gap-4 px-4 py-9 text-center">
          <h2 className="font-display text-lg font-bold">Dostavljamo u tvoj kraj</h2>
          <div className="flex flex-wrap justify-center gap-2">
            {["Centar", "Borik", "Lauš", "Starčevica"].map((z) => (
              <span
                key={z}
                className="rounded-full bg-white px-4 py-2 text-sm font-medium shadow-card"
              >
                {z}
              </span>
            ))}
          </div>
          <Link href="/products" className={cn(buttonVariants({ size: "md" }), "mt-1")}>
            Počni kupovinu
          </Link>
        </div>
      </section>
    </div>
  );
}

function Step({ icon, title, text }: { icon: React.ReactNode; title: string; text: string }) {
  return (
    <div className="flex items-start gap-3 rounded-card bg-white p-4 shadow-card">
      <div className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-brand-100 text-brand-700">
        {icon}
      </div>
      <div>
        <h3 className="font-display font-semibold">{title}</h3>
        <p className="mt-0.5 text-sm text-muted">{text}</p>
      </div>
    </div>
  );
}
