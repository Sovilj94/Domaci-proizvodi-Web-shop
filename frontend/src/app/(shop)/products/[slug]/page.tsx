"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowLeft, MapPin, ShoppingBasket, Sprout } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { QuantityStepper } from "@/components/ui/quantity-stepper";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { ProductGrid } from "@/components/shop/product-grid";
import { useProduct, useProducts } from "@/lib/api/hooks";
import { useCart } from "@/lib/cart/cart-context";
import { useToast } from "@/components/ui/toast";
import { formatPrice, unitSuffix } from "@/lib/utils";

export default function ProductDetailPage() {
  const slug = String(useParams().slug);
  const { data: product, isLoading, isError } = useProduct(slug);
  const related = useProducts({ category: product?.categorySlug });
  const { add } = useCart();
  const { show } = useToast();
  const [qty, setQty] = useState(1);

  if (isLoading) return <LoadingBlock />;
  if (isError || !product) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <EmptyState
          title="Proizvod nije pronađen"
          description="Možda je uklonjen ili je link neispravan."
          action={
            <Link href="/spajz">
              <Button variant="secondary">Nazad na proizvode</Button>
            </Link>
          }
        />
      </div>
    );
  }

  const soldOut = !product.isAvailable || product.stock <= 0;
  const relatedProducts =
    related.data?.filter((p) => p.id !== product.id).slice(0, 4) ?? [];

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <Link
        href="/spajz"
        className="inline-flex items-center gap-1.5 text-sm font-medium text-muted hover:text-ink"
      >
        <ArrowLeft className="h-4 w-4" /> Svi proizvodi
      </Link>

      <div className="mt-6 grid gap-8 md:grid-cols-2">
        <div className="relative aspect-square overflow-hidden rounded-card bg-sand shadow-card">
          <SmartImage src={product.imageUrl} alt={product.name} sizes="(max-width:768px) 100vw, 50vw" />
        </div>

        <div className="flex flex-col">
          <Link
            href="/spajz"
            className="text-sm font-semibold text-brand-700 hover:underline"
          >
            {product.categoryName}
          </Link>
          <h1 className="mt-1 font-display text-3xl font-bold">{product.name}</h1>

          <div className="mt-4 flex items-baseline gap-1">
            <span className="text-3xl font-bold">{formatPrice(product.price)}</span>
            <span className="text-muted">{unitSuffix(product.unit)}</span>
          </div>

          <p className="mt-4 text-ink/80">{product.description}</p>

          <div className="mt-6">
            {soldOut ? (
              <div className="rounded-card bg-sand px-4 py-3 text-sm font-medium text-muted">
                Trenutno nedostupno — provjeri ponovo sljedeće sedmice.
              </div>
            ) : (
              <div className="flex flex-wrap items-center gap-3">
                <QuantityStepper value={qty} onChange={setQty} max={product.stock} />
                <Button
                  size="lg"
                  onClick={() => {
                    add(product, qty);
                    show(`${product.name} (${qty} ${product.unit}) dodano u korpu`);
                  }}
                >
                  <ShoppingBasket className="h-5 w-5" /> Dodaj u korpu
                </Button>
              </div>
            )}
            {!soldOut && product.stock <= 10 && (
              <p className="mt-2 text-xs font-medium text-honey-600">
                Još samo {product.stock} {product.unit} na stanju.
              </p>
            )}
          </div>

          {/* Producer story */}
          {product.producer && (
            <div className="mt-8 rounded-card bg-surface p-5 shadow-card">
              <div className="flex items-center gap-3">
                <div className="relative h-14 w-14 overflow-hidden rounded-full bg-sand">
                  <SmartImage src={product.producer.photoUrl} alt={product.producer.name} sizes="56px" />
                </div>
                <div>
                  <p className="flex items-center gap-1 text-xs font-medium text-brand-700">
                    <Sprout className="h-3.5 w-3.5" /> Domaćin
                  </p>
                  <h3 className="font-display font-semibold">{product.producer.name}</h3>
                  <p className="flex items-center gap-1 text-xs text-muted">
                    <MapPin className="h-3 w-3" /> {product.producer.village}
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {relatedProducts.length > 0 && (
        <section className="mt-16">
          <h2 className="mb-5 font-display text-xl font-bold">Više iz kategorije {product.categoryName}</h2>
          <ProductGrid products={relatedProducts} />
        </section>
      )}
    </div>
  );
}
