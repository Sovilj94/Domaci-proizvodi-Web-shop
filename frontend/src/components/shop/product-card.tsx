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
 * Kartica bez okvira i sjenke: radijus i pozadina nose samo sliku, tekst stoji direktno
 * na pozadini sekcije. Okvir plus sjenka plus razmak su tri granice za istu stvar — kad
 * se dvije sklone, mreža proizvoda diše i slika radi posao.
 */
export function ProductCard({ product }: { product: Product }) {
  const { add } = useCart();
  const { show } = useToast();
  const soldOut = !product.isAvailable || product.stock <= 0;

  return (
    <div className="group flex h-full flex-col">
      <Link
        href={`/products/${product.slug}`}
        className="relative block aspect-square overflow-hidden rounded-card bg-sand"
      >
        <SmartImage
          src={product.imageUrl}
          alt={product.name}
          className="transition duration-300 group-hover:scale-105"
        />
        {product.isFeatured && !soldOut && (
          <span className="absolute left-3 top-3 rounded-md bg-ink px-2 py-1 text-[11px] font-bold uppercase tracking-wide text-white">
            Izdvojeno
          </span>
        )}
        {soldOut && (
          <div className="absolute inset-0 flex items-center justify-center bg-cream/70 text-sm font-semibold text-ink">
            Trenutno nedostupno
          </div>
        )}
      </Link>

      <div className="flex flex-1 flex-col pt-3">
        {/* Proizvođač u sitnim verzalima iznad naziva — nosilac hijerarhije ostaje naziv. */}
        <p className="truncate text-[11px] font-semibold uppercase tracking-wide text-muted">
          {product.producer?.name}
          {product.producer?.village ? ` · ${product.producer.village}` : ""}
        </p>
        <Link
          href={`/products/${product.slug}`}
          className="mt-1 font-display font-bold leading-tight text-ink hover:text-brand-700"
        >
          {product.name}
        </Link>

        <div className="mt-auto flex items-end justify-between pt-2">
          <div className="flex items-baseline gap-1">
            <span className="font-display text-lg font-bold text-ink">
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
            className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-brand-600 text-white transition hover:bg-brand-700 active:scale-90 disabled:cursor-not-allowed disabled:bg-sand disabled:text-muted"
          >
            <Plus className="h-5 w-5" />
          </button>
        </div>
      </div>
    </div>
  );
}
