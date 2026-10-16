"use client";

import Link from "next/link";
import type { Product } from "@/types/domain";
import { SmartImage } from "@/components/ui/smart-image";
import { useCart } from "@/lib/cart/cart-context";
import { flyToCart } from "@/lib/cart/fly-to-cart";
import { useToast } from "@/components/ui/toast";
import { formatPrice } from "@/lib/utils";

/**
 * Jedini izgled proizvoda u aplikaciji (R3, po uzoru na farmit Špajz): svijetlosiva
 * pozadina slike, naziv, cijena lijevo i pakovanje desno, narandžasto „Dodaj u korpu".
 * Koriste ga Špajz, „Dodaj iz Špajza", slični proizvodi i stranica domaćina — ako se
 * kartica negdje crta ručno, granica je pukla.
 */
export function ProductCard({ product }: { product: Product }) {
  const { add } = useCart();
  const { show } = useToast();
  const soldOut = !product.isAvailable || product.stock <= 0;

  return (
    <div className="group flex h-full flex-col overflow-hidden rounded-xl border border-line/70 bg-white">
      <Link href={`/products/${product.slug}`} className="relative m-1.5 block aspect-square overflow-hidden rounded-lg bg-sand">
        <SmartImage
          src={product.imageUrl}
          alt={product.name}
          className="transition duration-500 group-hover:scale-105"
        />
        {soldOut && (
          <div className="absolute inset-0 flex items-center justify-center bg-white/80 text-sm font-medium text-ink">
            Trenutno nedostupno
          </div>
        )}
      </Link>

      <div className="flex flex-1 flex-col px-3 pb-3 pt-2">
        <Link
          href={`/products/${product.slug}`}
          className="min-h-[2.5rem] text-sm leading-snug text-ink transition hover:text-honey-500"
        >
          {product.name}
        </Link>
        {product.producer?.name && <p className="mt-0.5 truncate text-[11px] text-muted">{product.producer.name}</p>}

        <div className="mt-auto flex items-baseline justify-between gap-2 pt-3">
          <span className="text-sm font-semibold text-ink">{formatPrice(product.price)}</span>
          <span className="text-sm text-ink">1 {product.unit}</span>
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
          className="mt-2.5 inline-flex h-10 w-full items-center justify-center rounded-full bg-honey-500 text-sm font-semibold text-white transition hover:bg-honey-600 active:scale-[0.98] disabled:cursor-not-allowed disabled:bg-honey-500/35"
        >
          {soldOut ? "Nema na stanju" : "Dodaj u korpu"}
        </button>
      </div>
    </div>
  );
}
