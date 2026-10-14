"use client";

import { SmartImage } from "@/components/ui/smart-image";
import { SectionHeader } from "@/components/ui/section-header";
import { useProducers } from "@/lib/api/hooks";

export function Domacini() {
  const producers = useProducers();

  return (
    <>
      <SectionHeader naslov="Naši domaćini" opis="Ljudi koji stoje iza svakog proizvoda." />
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
            <div key={p.id} className="overflow-hidden rounded-card border border-line bg-surface">
              <div className="relative aspect-[4/3] bg-sand">
                <SmartImage src={p.photoUrl} alt={p.name} sizes="(max-width: 640px) 50vw, 25vw" />
              </div>
              <div className="p-3.5">
                <p className="text-sm font-semibold text-ink">{p.name}</p>
                <p className="mt-0.5 text-xs text-muted">{p.village}</p>
                {(p.artikli?.length ?? 0) > 0 && (
                  <p className="mt-1.5 text-xs font-medium text-brand-700">{p.artikli?.join(" · ")}</p>
                )}
                <p className="mt-1.5 line-clamp-2 text-xs text-muted">{p.description}</p>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <p className="text-muted">Podaci o domaćinima trenutno nisu dostupni.</p>
      )}
    </>
  );
}
