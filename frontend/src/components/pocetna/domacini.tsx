"use client";

import { SectionHeader } from "@/components/ui/section-header";
import { useProducers } from "@/lib/api/hooks";
import { DomacinKartica } from "./domacin-kartica";

export function Domacini() {
  const producers = useProducers();

  return (
    <>
      <SectionHeader
        naslov="Naši domaćini"
        opis="Ljudi koji stoje iza svakog proizvoda."
        link="/proizvodjaci"
      />
      {producers.isLoading ? (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
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
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:gap-4">
          {producers.data.slice(0, 8).map((p) => (
            <DomacinKartica key={p.id} producer={p} />
          ))}
        </div>
      ) : (
        <p className="text-muted">Podaci o domaćinima trenutno nisu dostupni.</p>
      )}
    </>
  );
}
