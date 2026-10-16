"use client";

import { PageHeader } from "@/components/ui/page-header";
import { AnalizaKartica } from "@/components/analize/analiza-kartica";
import { EmptyState } from "@/components/ui/empty-state";
import { useAnalize } from "@/lib/api/hooks";

export default function AnalizePage() {
  const { data, isLoading, isError } = useAnalize();

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:py-14">
      <PageHeader
        naslov="Analize"
        opis="Ovdje objavljujemo nalaze laboratorijskih analiza uzoraka naših domaćina — i one koji nisu usklađeni."
      />

      <div className="mt-8">
        {isLoading ? (
          <div className="grid gap-6 md:grid-cols-2" aria-busy="true">
            {[0, 1].map((i) => (
              <div key={i} className="h-56 animate-pulse rounded-card bg-sand" />
            ))}
          </div>
        ) : isError ? (
          <EmptyState title="Greška pri učitavanju" description="Pokušaj ponovo za koji trenutak." />
        ) : !data?.length ? (
          <EmptyState title="Još nema objavljenih analiza" />
        ) : (
          <div className="grid gap-6 md:grid-cols-2">
            {data.map((a) => (
              <AnalizaKartica key={a.id} analiza={a} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
