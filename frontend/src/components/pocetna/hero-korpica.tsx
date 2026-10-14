"use client";

import Image from "next/image";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { Odbrojavanje } from "@/components/pocetna/odbrojavanje";
import { useKorpiceList } from "@/lib/api/hooks";
import { cn, formatPrice } from "@/lib/utils";

/**
 * Hero o korpici (spec P4). Cijena „od" je najniža među DOSTUPNIM korpicama; kad nijedna
 * nije stigla ili nije dostupna, red sa cijenom se ne prikazuje — hero ne izmišlja broj.
 */
export function HeroKorpica() {
  const { data: korpice } = useKorpiceList();
  const dostupne = (korpice ?? []).filter((k) => k.dostupna);
  const odCijene = dostupne.length > 0 ? Math.min(...dostupne.map((k) => k.cijena)) : null;

  return (
    <div className="relative overflow-hidden rounded-card border border-line bg-surface">
      <div className="grid items-center gap-6 md:grid-cols-2">
        <div className="order-2 p-6 sm:p-10 md:order-1">
          <span className="text-xs font-bold uppercase tracking-[0.18em] text-brand-600">
            Korpica sa Manjače
          </span>
          <h1 className="mt-3 font-display text-3xl font-bold leading-[1.1] text-ink sm:text-5xl">
            Domaće namirnice,
            <br />
            svake sedmice na tvojim vratima.
          </h1>
          <p className="mt-4 max-w-md text-sm text-muted sm:text-base">
            Izaberi korpicu, a mi je spakujemo od onoga što je te sedmice najbolje. Plaćaš
            pouzećem.
          </p>
          {odCijene !== null && (
            <p className="mt-4 text-sm text-muted">
              već od <span className="text-2xl font-bold text-ink">{formatPrice(odCijene)}</span>
            </p>
          )}
          <div className="mt-6 flex flex-wrap gap-3">
            <Link href="/korpice" className={buttonVariants({ size: "lg" })}>
              Izaberi korpicu
              <ArrowRight className="h-4 w-4" />
            </Link>
            <Link href="/products" className={cn(buttonVariants({ size: "lg", variant: "secondary" }))}>
              Svi proizvodi
            </Link>
          </div>
          <div className="mt-6 max-w-md">
            <Odbrojavanje />
          </div>
        </div>
        <div className="relative order-1 h-56 md:order-2 md:h-full md:min-h-[460px]">
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
  );
}
