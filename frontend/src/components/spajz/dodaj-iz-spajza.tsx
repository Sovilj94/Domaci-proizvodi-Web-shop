"use client";

import Link from "next/link";
import { Karusel } from "@/components/pocetna/karusel";
import { ProductCard } from "@/components/shop/product-card";
import { useProducts } from "@/lib/api/hooks";

/**
 * „Dodaj iz Špajza" (R3): izdvojeni proizvodi uz korpicu — sir, suho meso, ajvar, zimnica.
 * Isti „Dodaj u korpu", pa roba ide u istu narudžbu i istu dostavu kao korpica.
 * Bez dostupnih izdvojenih proizvoda blok se ne prikazuje.
 */
export function DodajIzSpajza({ naslov = "Dodaj iz Špajza" }: { naslov?: string }) {
  const { data } = useProducts({ featured: true });
  const dostupni = (data ?? []).filter((p) => p.isAvailable && p.stock > 0).slice(0, 8);
  if (dostupni.length === 0) return null;

  return (
    <section className="px-4 py-12">
      <div className="mx-auto max-w-6xl">
        <div className="mb-2 flex items-end justify-between gap-4">
          <div>
            <h2 className="text-xl font-semibold text-ink sm:text-2xl">{naslov}</h2>
            <p className="text-sm text-ink/80">Stiže u istoj dostavi kao korpica.</p>
          </div>
          <Link href="/spajz" className="shrink-0 text-sm text-muted hover:text-ink">
            Cijeli Špajz
          </Link>
        </div>
        <Karusel stavka="w-[46%] sm:w-[31%] lg:w-[calc((100%-6rem)/5)]" strelice={false}>
          {dostupni.map((p) => (
            <ProductCard key={p.id} product={p} />
          ))}
        </Karusel>
      </div>
    </section>
  );
}
