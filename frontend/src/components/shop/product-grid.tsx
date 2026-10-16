import type { Product } from "@/types/domain";
import { Reveal } from "@/components/ui/reveal";
import { ProductCard } from "./product-card";

/* Mreža Špajza po uzoru na farmit (R3): 2 kolone na telefonu, do 6 na širokom ekranu. */
const MREZA = "grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5 xl:grid-cols-6 lg:gap-4";

export function ProductGrid({ products }: { products: Product[] }) {
  return (
    <div className={MREZA}>
      {products.map((p, i) => (
        <Reveal key={p.id} className="h-full" delay={Math.min(i, 8) * 0.04}>
          <ProductCard product={p} />
        </Reveal>
      ))}
    </div>
  );
}

export function ProductGridSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className={MREZA}>
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="rounded-xl border border-line/70 p-1.5">
          <div className="skeleton aspect-square rounded-lg" />
          <div className="space-y-2 p-2">
            <div className="skeleton h-3 w-2/3 rounded" />
            <div className="skeleton h-4 w-full rounded" />
            <div className="skeleton h-9 w-full rounded-full" />
          </div>
        </div>
      ))}
    </div>
  );
}
