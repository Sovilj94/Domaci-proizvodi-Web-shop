"use client";

import { DodajIzSpajza } from "@/components/spajz/dodaj-iz-spajza";
import Link from "next/link";
import { ShoppingBasket, Trash2 } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { QuantityStepper } from "@/components/ui/quantity-stepper";
import { Button, buttonVariants } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { useCart } from "@/lib/cart/cart-context";
import { formatPrice, unitSuffix, cn } from "@/lib/utils";

export default function CartPage() {
  const { items, subtotal, setQuantity, remove, hydrated } = useCart();

  if (!hydrated) return null;

  if (items.length === 0) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12">
        <EmptyState
          icon={<ShoppingBasket className="h-7 w-7" />}
          title="Korpa je prazna"
          description="Dodaj domaće proizvode sa Manjače i vrati se ovdje."
          action={
            <Link href="/spajz" className={cn(buttonVariants())}>
              Pogledaj proizvode
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-display text-2xl font-bold md:text-3xl">Korpa</h1>

      <div className="mt-6 grid gap-8 lg:grid-cols-[1fr_20rem]">
        <ul className="space-y-3">
          {items.map((item) => (
            <li
              key={item.productId}
              className="flex items-center gap-4 rounded-card bg-surface p-3 border border-line"
            >
              <Link
                href={`/products/${item.slug}`}
                className="relative h-20 w-20 shrink-0 overflow-hidden rounded-xl bg-sand"
              >
                <SmartImage src={item.imageUrl} alt={item.name} sizes="80px" />
              </Link>
              <div className="min-w-0 flex-1">
                <Link
                  href={`/products/${item.slug}`}
                  className="font-display font-semibold hover:text-brand-700"
                >
                  {item.name}
                </Link>
                <p className="text-sm text-muted">
                  {formatPrice(item.price)}
                  {unitSuffix(item.unit)}
                </p>
                <div className="mt-2">
                  <QuantityStepper
                    size="sm"
                    value={item.quantity}
                    onChange={(q) => setQuantity(item.productId, q)}
                    max={item.stock}
                  />
                </div>
              </div>
              <div className="flex flex-col items-end gap-3">
                <span className="font-bold">{formatPrice(item.price * item.quantity)}</span>
                <button
                  onClick={() => remove(item.productId)}
                  aria-label="Ukloni"
                  className="text-muted transition hover:text-red-600"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </li>
          ))}
        </ul>

        <aside className="h-fit rounded-card bg-surface p-5 shadow-card lg:sticky lg:top-28">
          <h2 className="font-display text-lg font-semibold">Pregled</h2>
          <dl className="mt-4 space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-muted">Međuzbir</dt>
              <dd className="font-medium">{formatPrice(subtotal)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-muted">Dostava</dt>
              <dd className="text-muted">računa se na naplati</dd>
            </div>
          </dl>
          <div className="mt-4 flex items-center justify-between border-t border-line pt-4">
            <span className="font-semibold">Ukupno (bez dostave)</span>
            <span className="font-display text-xl font-bold">{formatPrice(subtotal)}</span>
          </div>
          <Link href="/checkout/nalog" className="mt-5 block">
            <Button className="w-full" size="lg">
              Nastavi na dostavu
            </Button>
          </Link>
          <Link
            href="/spajz"
            className="mt-3 block text-center text-sm font-medium text-muted hover:text-ink"
          >
            Nastavi kupovinu
          </Link>
        </aside>
      </div>

      {/* R3: dodaci iz Špajza stižu u istoj dostavi kao korpica. */}
      <div className="-mx-4 mt-6">
        <DodajIzSpajza naslov="Možda ti zatreba i ovo" />
      </div>
    </div>
  );
}
