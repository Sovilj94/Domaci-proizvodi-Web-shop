"use client";

import { ShoppingBasket } from "lucide-react";
import { useCart } from "@/lib/cart/cart-context";

export function CartButton() {
  const { count, hydrated, openCart } = useCart();
  return (
    <button
      onClick={openCart}
      aria-label="Korpa"
      className="relative grid h-10 w-10 place-items-center rounded-full text-ink transition hover:bg-sand"
    >
      <ShoppingBasket className="h-5 w-5" />
      {hydrated && count > 0 && (
        <span className="absolute -right-0.5 -top-0.5 grid h-5 min-w-5 place-items-center rounded-full bg-honey-500 px-1 text-[11px] font-bold text-ink">
          {count}
        </span>
      )}
    </button>
  );
}
