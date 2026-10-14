"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { ProductGrid, ProductGridSkeleton } from "@/components/shop/product-grid";
import { useProducer, useProducts } from "@/lib/api/hooks";

export default function ProizvodjacDetaljPage() {
  const id = String(useParams<{ id: string }>().id);
  const { data: producer, isLoading, isError } = useProducer(id);
  const products = useProducts({ producer: id });

  if (isLoading) return <LoadingBlock />;
  if (isError || !producer) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-12">
        <EmptyState
          title="Domaćin nije pronađen"
          description="Možda je uklonjen ili je link pogrešan."
          action={
            <Link href="/proizvodjaci">
              <Button variant="secondary">← Svi domaćini</Button>
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <div className="grid gap-8 md:grid-cols-2">
        <div className="relative aspect-[4/3] overflow-hidden rounded-card bg-sand shadow-card">
          <SmartImage src={producer.photoUrl} alt={producer.name} sizes="(max-width:768px) 100vw, 50vw" />
        </div>
        <div className="flex flex-col">
          <h1 className="font-display text-3xl font-bold">{producer.name}</h1>
          <p className="mt-1 text-muted">{producer.village}</p>
          {producer.description && (
            <p className="mt-4 whitespace-pre-line text-ink/80">{producer.description}</p>
          )}
        </div>
      </div>

      <section className="mt-12">
        <h2 className="mb-6 font-display text-2xl font-bold text-ink">Iz njegove ponude</h2>
        {products.isLoading ? (
          <ProductGridSkeleton count={4} />
        ) : products.data?.length ? (
          <ProductGrid products={products.data} />
        ) : (
          <p className="text-muted">Trenutno nema artikala u ponudi.</p>
        )}
      </section>

      {/* Analize — Task 8 */}

      <Link
        href="/proizvodjaci"
        className="mt-10 inline-flex items-center gap-1.5 text-sm font-medium text-muted hover:text-ink"
      >
        <ArrowLeft className="h-4 w-4" /> Svi domaćini
      </Link>
    </div>
  );
}
