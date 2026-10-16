"use client";

import { ShoppingCart } from "lucide-react";
import { useCart } from "@/lib/cart/cart-context";
import { cn } from "@/lib/utils";

/** Ikona korpe u zaglavlju (farmit: kolica). `className` nosi boju — bijela preko hero slike. */
export function CartButton({ className }: { className?: string }) {
  const { count, hydrated, openCart } = useCart();
  return (
    <button
      onClick={openCart}
      aria-label="Korpa"
      className={cn(
        "relative grid h-10 w-10 place-items-center rounded-full text-ink transition hover:bg-black/5",
        className,
      )}
    >
      <ShoppingCart className="h-6 w-6" />
      {hydrated && count > 0 && (
        <span
          id="cart-target"
          className="absolute -right-0.5 -top-0.5 grid h-5 min-w-5 place-items-center rounded-full bg-honey-500 px-1 text-[11px] font-bold text-white"
        >
          {count}
        </span>
      )}
    </button>
  );
}
