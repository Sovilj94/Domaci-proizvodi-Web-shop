"use client";

import { SectionHeader } from "@/components/ui/section-header";
import { EmptyState } from "@/components/ui/empty-state";
import { DomacinKartica } from "@/components/pocetna/domacin-kartica";
import { useProducers } from "@/lib/api/hooks";

const MREZA = "grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-4 lg:gap-4";

export default function ProizvodjaciPage() {
  const producers = useProducers();

  return (
    <div className="mx-auto max-w-6xl px-4 py-10">
      <SectionHeader
        naslov="Naši domaćini"
        opis="Porodice i gazdinstva od kojih dolazi ono što prodajemo."
      />
      {producers.isLoading ? (
        <div className={MREZA}>
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="overflow-hidden rounded-card border border-line bg-surface">
              <div className="skeleton aspect-[4/3]" />
              <div className="p-3.5">
                <div className="skeleton h-3 w-20 rounded" />
                <div className="skeleton mt-2 h-3 w-14 rounded" />
              </div>
            </div>
          ))}
        </div>
      ) : producers.data?.length ? (
        <div className={MREZA}>
          {producers.data.map((p) => (
            <DomacinKartica key={p.id} producer={p} />
          ))}
        </div>
      ) : (
        <EmptyState title="Još nema domaćina" />
      )}
    </div>
  );
}
