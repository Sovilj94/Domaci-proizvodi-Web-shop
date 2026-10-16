"use client";

import { PageHeader } from "@/components/ui/page-header";
import { ReceptKartica } from "@/components/recepti/recept-kartica";
import { EmptyState } from "@/components/ui/empty-state";
import { useRecepti } from "@/lib/api/hooks";

export default function ReceptiPage() {
  const { data, isLoading, isError } = useRecepti();

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:py-14">
      <PageHeader naslov="Recepti" opis="Šta skuhati od onoga što stigne u korpici." />

      <div className="mt-8">
        {isLoading ? (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3" aria-busy="true">
            {[0, 1, 2].map((i) => (
              <div key={i} className="h-72 animate-pulse rounded-card bg-sand" />
            ))}
          </div>
        ) : isError ? (
          <EmptyState title="Greška pri učitavanju" description="Pokušaj ponovo za koji trenutak." />
        ) : !data?.length ? (
          <EmptyState title="Još nema recepata" />
        ) : (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {data.map((r) => (
              <ReceptKartica key={r.id} recept={r} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
