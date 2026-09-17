"use client";

import Link from "next/link";
import { Plus } from "lucide-react";
import type { Product } from "@/types/domain";
import { SmartImage } from "@/components/ui/smart-image";
import { useCart } from "@/lib/cart/cart-context";
import { flyToCart } from "@/lib/cart/fly-to-cart";
import { useToast } from "@/components/ui/toast";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";

/**
 * Kartica u dvije podloge. `tone="dark"` je za sekciju izdvojenih proizvoda, gdje
 * kartica stoji na `night` i mora da se odvoji sopstvenom pozadinom.
 *
 * Boje se ne izvode iz `tone` inline nego iz ove dvije tabele, jer svaki par ovdje
 * je provjeren na kontrast: `muted` na tamnoj daje 4.03:1 i pada AA, zato tamna
 * varijanta ima svoj `muted-dark` umjesto da dijeli isti token.
 */
const tones = {
  light: {
    okvir: "bg-transparent",
    slika: "bg-sand",
    naziv: "text-ink hover:text-brand-700",
    proizvodjac: "text-muted",
    cijena: "text-ink",
    jedinica: "text-muted",
    linija: "border-line",
    znacka: "bg-ink/85 text-cream",
    kategorija: "bg-cream/90 text-ink",
    nedostupno: "bg-cream/80 text-ink",
  },
  dark: {
    okvir: "bg-night-card rounded-card p-3",
    slika: "bg-night",
    naziv: "text-cream hover:text-honey-500",
    proizvodjac: "text-muted-dark",
    cijena: "text-cream",
    jedinica: "text-muted-dark",
    linija: "border-white/10",
    znacka: "bg-honey-500 text-ink",
    kategorija: "bg-night/80 text-cream",
    nedostupno: "bg-night/85 text-cream",
  },
} as const;

export function ProductCard({
  product,
  tone = "light",
}: {
  product: Product;
  tone?: "light" | "dark";
}) {
  const { add } = useCart();
  const { show } = useToast();
  const soldOut = !product.isAvailable || product.stock <= 0;
  const t = tones[tone];

  return (
    <div className={cn("group flex h-full flex-col", t.okvir)}>
      <Link
        href={`/products/${product.slug}`}
        className={cn("relative block aspect-[4/3] overflow-hidden rounded-card", t.slika)}
      >
        <SmartImage
          src={product.imageUrl}
          alt={product.name}
          className="transition duration-500 group-hover:scale-105"
        />
        {product.isFeatured && !soldOut && (
          <span
            className={cn(
              "absolute left-3 top-3 rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-widest",
              t.znacka,
            )}
          >
            Izdvojeno
          </span>
        )}
        {product.categoryName && (
          <span
            className={cn(
              "absolute right-3 top-3 rounded-full px-2.5 py-1 text-[11px] font-medium backdrop-blur",
              t.kategorija,
            )}
          >
            {product.categoryName}
          </span>
        )}
        {soldOut && (
          <div
            className={cn(
              "absolute inset-0 flex items-center justify-center text-sm font-medium",
              t.nedostupno,
            )}
          >
            Trenutno nedostupno
          </div>
        )}
      </Link>

      <div className="flex flex-1 flex-col pt-3.5">
        <Link
          href={`/products/${product.slug}`}
          className={cn("font-display text-lg font-bold leading-tight transition", t.naziv)}
        >
          {product.name}
        </Link>
        <p className={cn("mt-1 truncate text-xs", t.proizvodjac)}>
          {product.producer?.name}
          {product.producer?.village ? `, ${product.producer.village}` : ""}
        </p>

        <div className={cn("mt-auto flex items-end justify-between border-t pt-3.5", t.linija)}>
          <div className="flex items-baseline gap-1">
            <span className={cn("font-display text-xl font-bold", t.cijena)}>
              {formatPrice(product.price)}
            </span>
            <span className={cn("text-xs", t.jedinica)}>{unitSuffix(product.unit)}</span>
          </div>
          <button
            type="button"
            disabled={soldOut}
            onClick={(e) => {
              const btn = e.currentTarget;
              add(product);
              show(`${product.name} dodano u korpu`);
              requestAnimationFrame(() => flyToCart(btn));
            }}
            aria-label={`Dodaj ${product.name} u korpu`}
            className="inline-flex h-10 shrink-0 items-center gap-1.5 rounded-full bg-brand-600 px-3 text-sm font-medium text-white transition hover:bg-brand-700 active:scale-90 disabled:cursor-not-allowed disabled:bg-brand-600/30 disabled:text-white/60 sm:px-4"
          >
            <Plus className="h-4 w-4" />
            {/* Na uskoj mobilnoj traci kartica je 160px — natpis bi gurnuo cijenu u prelom. */}
            <span className="hidden sm:inline">Dodaj</span>
          </button>
        </div>
      </div>
    </div>
  );
}
