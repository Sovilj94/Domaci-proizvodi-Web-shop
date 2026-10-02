import type { Product } from "@/types/domain";
import { Reveal } from "@/components/ui/reveal";
import { ProductCard } from "./product-card";

/* Kartica nosi vlastiti border, pa razmak više ne mora da je razdvaja — gap-8 je
   ostao iz izgleda u kojem kartica nije imala okvir. Mockup drži kartice blizu. */
const MREZA = "grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4 lg:gap-4";

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

export function ProductGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className={MREZA}>
      {Array.from({ length: count }).map((_, i) => (
        <div key={i}>
          <div className="skeleton aspect-square rounded-card" />
          <div className="space-y-2 pt-3">
            <div className="skeleton h-3 w-2/3 rounded" />
            <div className="skeleton h-4 w-full rounded" />
            <div className="skeleton h-5 w-1/2 rounded" />
          </div>
        </div>
      ))}
    </div>
  );
}
