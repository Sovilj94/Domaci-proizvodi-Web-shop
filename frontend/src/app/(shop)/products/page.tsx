"use client";

import { useEffect, useState } from "react";
import { LayoutGrid, Search } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { ProductGrid, ProductGridSkeleton } from "@/components/shop/product-grid";
import { EmptyState } from "@/components/ui/empty-state";
import { useCategories, useProducts } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

export default function ProductsPage() {
  const [category, setCategory] = useState<string | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const search = useDebounced(searchInput, 250);

  // Preselect the category / search term when arriving from a tile or the navbar search
  // (?category=slug, ?search=term).
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const c = params.get("category");
    if (c) setCategory(c);
    const s = params.get("search");
    if (s) setSearchInput(s);
  }, []);

  const categories = useCategories();
  const products = useProducts({
    category: category ?? undefined,
    search: search || undefined,
  });

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <div className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-bold md:text-3xl">
          {categories.data?.find((c) => c.slug === category)?.name ?? "Proizvodi"}
        </h1>
        <p className="text-muted">Svježe sa Manjače, birano za tvoju sedmicu.</p>
      </div>

      <div className="sticky top-16 z-30 -mx-4 mt-5 bg-cream/90 px-4 py-3 backdrop-blur">
        <div className="relative mb-3">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Traži proizvod…"
            className="w-full rounded-full border-none bg-surface py-2.5 pl-10 pr-4 text-sm ring-1 ring-line focus:outline-none focus:ring-2 focus:ring-brand-400"
          />
        </div>
        {categories.data && (
          <div className="no-scrollbar -mx-4 flex gap-3 overflow-x-auto px-4 pb-1">
            <button
              type="button"
              onClick={() => setCategory(null)}
              className="w-16 shrink-0 text-center sm:w-20"
            >
              <div
                className={cn(
                  "grid aspect-square place-items-center rounded-2xl bg-sand transition",
                  category === null ? "ring-2 ring-brand-500" : "border border-line",
                )}
              >
                <LayoutGrid className="h-6 w-6 text-brand-700" />
              </div>
              <p
                className={cn(
                  "mt-1.5 text-xs font-medium",
                  category === null ? "text-brand-700" : "text-ink",
                )}
              >
                Sve
              </p>
            </button>
            {categories.data.map((c) => {
              const active = category === c.slug;
              return (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => setCategory(c.slug)}
                  className="group w-16 shrink-0 text-center sm:w-20"
                >
                  <div
                    className={cn(
                      "relative aspect-square overflow-hidden rounded-2xl bg-sand transition",
                      active ? "ring-2 ring-brand-500" : "border border-line",
                    )}
                  >
                    <SmartImage
                      src={c.imageUrl}
                      alt={c.name}
                      sizes="80px"
                      className="transition duration-300 group-hover:scale-105"
                    />
                  </div>
                  <p
                    className={cn(
                      "mt-1.5 truncate text-xs font-medium",
                      active ? "text-brand-700" : "text-ink",
                    )}
                  >
                    {c.name}
                  </p>
                </button>
              );
            })}
          </div>
        )}
      </div>

      <div className="mt-6">
        {products.isLoading ? (
          <ProductGridSkeleton />
        ) : products.isError ? (
          <EmptyState
            title="Greška pri učitavanju"
            description="Nismo uspjeli dohvatiti proizvode. Pokušajte ponovo."
          />
        ) : products.data && products.data.length > 0 ? (
          <>
            <p className="mb-4 text-sm text-muted">{products.data.length} proizvoda</p>
            <ProductGrid products={products.data} />
          </>
        ) : (
          <EmptyState
            title="Nema rezultata"
            description="Pokušajte drugu kategoriju ili pojam pretrage."
          />
        )}
      </div>
    </div>
  );
}

function useDebounced<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}
