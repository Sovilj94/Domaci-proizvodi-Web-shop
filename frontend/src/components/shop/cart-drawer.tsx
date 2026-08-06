"use client";

import { useEffect } from "react";
import Link from "next/link";
import { ShoppingBasket, Trash2, X } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { QuantityStepper } from "@/components/ui/quantity-stepper";
import { Button, buttonVariants } from "@/components/ui/button";
import { useCart } from "@/lib/cart/cart-context";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";

/**
 * Slide-over cart (Glovo/Wolt style) — opens from the cart button / bottom-nav on any
 * page. Full-width sheet on phones, side panel on desktop. Keeps /cart as a fallback page.
 */
export function CartDrawer() {
  const { items, subtotal, setQuantity, remove, isOpen, closeCart, hydrated } = useCart();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && closeCart();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [closeCart]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50">
      <button aria-label="Zatvori" onClick={closeCart} className="absolute inset-0 bg-black/40" />
      <div className="absolute right-0 top-0 flex h-full w-full max-w-md flex-col bg-cream shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-5 py-4">
          <h2 className="font-display text-lg font-bold">Korpa</h2>
          <button
            onClick={closeCart}
            aria-label="Zatvori"
            className="rounded-full p-1.5 transition hover:bg-sand"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {!hydrated ? null : items.length === 0 ? (
          <div className="flex flex-1 flex-col items-center justify-center gap-3 p-8 text-center">
            <div className="grid h-14 w-14 place-items-center rounded-full bg-sand text-muted">
              <ShoppingBasket className="h-7 w-7" />
            </div>
            <p className="font-medium">Korpa je prazna</p>
            <Link
              href="/products"
              onClick={closeCart}
              className={cn(buttonVariants({ variant: "secondary" }))}
            >
              Pogledaj proizvode
            </Link>
          </div>
        ) : (
          <>
            <ul className="flex-1 space-y-3 overflow-y-auto p-4">
              {items.map((item) => (
                <li
                  key={item.productId}
                  className="flex gap-3 rounded-card bg-white p-3 border border-line"
                >
                  <div className="relative h-16 w-16 shrink-0 overflow-hidden rounded-xl bg-sand">
                    <SmartImage src={item.imageUrl} alt={item.name} sizes="64px" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-display text-sm font-semibold">{item.name}</p>
                    <p className="text-xs text-muted">
                      {formatPrice(item.price)}
                      {unitSuffix(item.unit)}
                    </p>
                    <div className="mt-1.5 flex items-center justify-between gap-2">
                      <QuantityStepper
                        size="sm"
                        value={item.quantity}
                        onChange={(q) => setQuantity(item.productId, q)}
                        max={item.stock}
                      />
                      <span className="text-sm font-bold">
                        {formatPrice(item.price * item.quantity)}
                      </span>
                    </div>
                  </div>
                  <button
                    onClick={() => remove(item.productId)}
                    aria-label="Ukloni"
                    className="self-start text-muted transition hover:text-red-600"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </li>
              ))}
            </ul>

            <div className="border-t border-line bg-white p-4">
              <div className="flex items-center justify-between">
                <span className="font-semibold">Ukupno (bez dostave)</span>
                <span className="font-display text-xl font-bold">{formatPrice(subtotal)}</span>
              </div>
              <Link href="/checkout" onClick={closeCart} className="mt-3 block">
                <Button className="w-full" size="lg">
                  Nastavi na dostavu
                </Button>
              </Link>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
