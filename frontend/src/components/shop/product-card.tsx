"use client";

import Link from "next/link";
import { Plus } from "lucide-react";
import type { Product } from "@/types/domain";
import { SmartImage } from "@/components/ui/smart-image";
import { useCart } from "@/lib/cart/cart-context";
import { useToast } from "@/components/ui/toast";
import { formatPrice, unitSuffix } from "@/lib/utils";

export function ProductCard({ product }: { product: Product }) {
  const { add } = useCart();
  const { show } = useToast();
  const soldOut = !product.isAvailable || product.stock <= 0;

  return (
    <div className="group flex flex-col overflow-hidden rounded-card bg-white ring-1 ring-black/5 transition hover:shadow-md">
      <Link
        href={`/products/${product.slug}`}
        className="relative block aspect-square overflow-hidden bg-sand"
      >
        <SmartImage
          src={product.imageUrl}
          alt={product.name}
          className="transition duration-300 group-hover:scale-105"
        />
        {product.isFeatured && !soldOut && (
          <span className="absolute left-3 top-3 rounded-full bg-honey-500 px-2.5 py-1 text-[11px] font-bold text-ink">
            Izdvojeno
          </span>
        )}
        {soldOut && (
          <div className="absolute inset-0 flex items-center justify-center bg-white/70 text-sm font-semibold text-ink">
            Trenutno nedostupno
          </div>
        )}
      </Link>

      <div className="flex flex-1 flex-col p-4">
        <p className="truncate text-xs text-muted">
          {product.producer?.name} · {product.producer?.village}
        </p>
        <Link
          href={`/products/${product.slug}`}
          className="mt-0.5 font-display font-semibold leading-tight text-ink hover:text-brand-700"
        >
          {product.name}
        </Link>

        <div className="mt-auto flex items-end justify-between pt-3">
          <div>
            <span className="text-lg font-bold text-ink">{formatPrice(product.price)}</span>
            <span className="text-xs text-muted">{unitSuffix(product.unit)}</span>
          </div>
          <button
            type="button"
            disabled={soldOut}
            onClick={() => {
              add(product);
              show(`${product.name} dodano u korpu`);
            }}
            aria-label={`Dodaj ${product.name} u korpu`}
            className="grid h-10 w-10 place-items-center rounded-full bg-brand-600 text-white transition hover:bg-brand-700 disabled:cursor-not-allowed disabled:bg-zinc-200 disabled:text-zinc-400"
          >
            <Plus className="h-5 w-5" />
          </button>
        </div>
      </div>
    </div>
  );
}
