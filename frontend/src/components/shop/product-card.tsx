"use client";

import Link from "next/link";
import { Plus } from "lucide-react";
import type { Product } from "@/types/domain";
import { SmartImage } from "@/components/ui/smart-image";
import { useCart } from "@/lib/cart/cart-context";
import { flyToCart } from "@/lib/cart/fly-to-cart";
import { useToast } from "@/components/ui/toast";
import { formatPrice, unitSuffix } from "@/lib/utils";

/**
 * Jedini izgled proizvoda u aplikaciji — koriste ga početna, /products, „Slični
 * proizvodi" i (od Faze 5) Omiljeni. Ako se kartica negdje crta ručno, granica je pukla.
 *
 * Gornji desni ugao slike je namjerno prazan: tu ide srce za omiljene, koje stiže u
 * Fazi 5. Mjesto mu je apsolutno pozicionirano pa se raspored neće pomjeriti kad se
 * uključi. Do tada se ne renderuje ništa — onemogućeno srce koje ništa ne radi je gore
 * od praznog ugla.
 */
export function ProductCard({ product }: { product: Product }) {
  const { add } = useCart();
  const { show } = useToast();
  const soldOut = !product.isAvailable || product.stock <= 0;

  return (
    <div className="group flex h-full flex-col overflow-hidden rounded-card border border-line bg-surface transition hover:shadow-md">
      <Link
        href={`/products/${product.slug}`}
        className="relative block aspect-square overflow-hidden bg-sand"
      >
        <SmartImage
          src={product.imageUrl}
          alt={product.name}
          className="transition duration-500 group-hover:scale-105"
        />
        <span className="absolute left-2.5 top-2.5 rounded-full bg-surface/90 px-2.5 py-1 text-[11px] font-medium text-ink backdrop-blur">
          {product.isFeatured ? "Izdvojeno" : product.categoryName}
        </span>
        {soldOut && (
          <div className="absolute inset-0 flex items-center justify-center bg-surface/80 text-sm font-medium text-ink">
            Trenutno nedostupno
          </div>
        )}
      </Link>

      <div className="flex flex-1 flex-col p-3 sm:p-3.5">
        <Link
          href={`/products/${product.slug}`}
          className="text-sm font-semibold leading-snug text-ink transition hover:text-brand-700 sm:text-base"
        >
          {product.name}
        </Link>
        <p className="mt-0.5 truncate text-xs text-muted">
          {product.producer?.name}
          {product.producer?.village ? `, ${product.producer.village}` : ""}
        </p>

        <div className="mt-3 flex items-baseline gap-0.5">
          <span className="text-base font-bold text-ink sm:text-lg">
            {formatPrice(product.price)}
          </span>
          <span className="text-xs text-muted">{unitSuffix(product.unit)}</span>
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
          className="mt-3 inline-flex h-10 w-full items-center justify-center gap-1.5 rounded-[10px] bg-brand-600 text-sm font-medium text-white transition hover:bg-brand-700 active:scale-[0.98] disabled:cursor-not-allowed disabled:bg-brand-600/30 disabled:text-white/70"
        >
          <Plus className="h-4 w-4" />
          {/*
            Na telefonu je kartica ~160px i natpis bi se prelomio, pa ostaje samo „+".
            Dugme je i dalje puna širina — tap target je cijela donja ivica kartice.
          */}
          <span className="hidden sm:inline">{soldOut ? "Nema na stanju" : "Dodaj u korpu"}</span>
        </button>
      </div>
    </div>
  );
}
