"use client";

import { useState } from "react";
import Link from "next/link";
import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { Zvjezdice } from "@/components/ocjena/zvjezdice";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { queryKeys } from "@/lib/api/hooks";
import { ocjeneRijec } from "@/lib/orders";
import { cn } from "@/lib/utils";
import type { OcjenaAdmin } from "@/types/domain";

const TABOVI: { vrijednost?: number; naziv: string }[] = [
  { naziv: "Sve" },
  { vrijednost: 5, naziv: "5" },
  { vrijednost: 4, naziv: "4" },
  { vrijednost: 3, naziv: "3" },
  { vrijednost: 2, naziv: "2" },
  { vrijednost: 1, naziv: "1" },
];

function datum(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "" : d.toLocaleDateString("sr-Latn-BA");
}

function Red({ o }: { o: OcjenaAdmin }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const napravi = useMutation({
    mutationFn: () => adminApi.napraviUtisak(o.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "ocjene"] });
      qc.invalidateQueries({ queryKey: queryKeys.adminUtisci() });
      show("Utisak napravljen — objavite ga na stranici Utisci");
    },
    onError: (err) =>
      show(err instanceof ApiRequestError ? err.message : "Pravljenje utiska nije uspjelo", "error"),
  });

  const imaKomentar = o.komentar.trim().length > 0;
  const dovoljnoZaUtisak = o.komentar.trim().length >= 10;

  return (
    <li className="space-y-2 p-4">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
        <Zvjezdice ocjena={o.ocjena} />
        {o.dozvolaZaObjavu && (
          <span className="rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700">
            Dozvoljena objava
          </span>
        )}
      </div>
      {imaKomentar ? (
        <p className="whitespace-pre-line text-sm text-ink">{o.komentar}</p>
      ) : (
        <p className="text-sm text-muted">bez komentara</p>
      )}
      <p className="text-sm text-muted">
        {o.kodNarudzbe} · {o.kupac} · {o.zona} · {datum(o.kreirana)}
      </p>
      {o.utisakId ? (
        <p className="text-sm">
          <span className="text-muted">Utisak napravljen · </span>
          <Link href="/admin/utisci" className="font-medium text-brand-700 underline">
            Utisci
          </Link>
        </p>
      ) : (
        o.dozvolaZaObjavu &&
        (dovoljnoZaUtisak ? (
          <Button size="sm" onClick={() => napravi.mutate()} disabled={napravi.isPending}>
            {napravi.isPending ? "Pravim…" : "Napravi utisak"}
          </Button>
        ) : (
          <p className="text-sm text-muted">Komentar je prekratak za utisak.</p>
        ))
      )}
    </li>
  );
}

export default function AdminOcjenePage() {
  const [filter, setFilter] = useState<number | undefined>(undefined);
  const podaci = useQuery({
    queryKey: queryKeys.adminOcjene(filter),
    queryFn: () => adminApi.ocjene(filter),
    placeholderData: keepPreviousData,
  });
  const sazetak = podaci.data?.sazetak;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold">Ocjene isporuke</h1>
        <p className="text-muted">Šta kupci kažu nakon isporuke.</p>
      </div>

      {sazetak && (
        <section
          aria-label="Sažetak ocjena"
          className="grid gap-6 rounded-card bg-white p-5 shadow-card sm:grid-cols-[auto_1fr]"
        >
          <div className="space-y-1">
            <p className="font-display text-4xl font-bold">
              {sazetak.broj > 0
                ? sazetak.prosjek.toLocaleString("bs-BA", {
                    minimumFractionDigits: 1,
                    maximumFractionDigits: 1,
                  })
                : "–"}
            </p>
            <Zvjezdice ocjena={Math.round(sazetak.prosjek)} ariaHidden />
            <p className="text-sm text-muted">
              {sazetak.broj} {ocjeneRijec(sazetak.broj)}
            </p>
          </div>
          <ul className="space-y-1.5" aria-label="Raspodjela ocjena">
            {[5, 4, 3, 2, 1].map((n) => {
              const broj = sazetak.poBroju[n - 1] ?? 0;
              const udio = sazetak.broj > 0 ? (broj / sazetak.broj) * 100 : 0;
              return (
                <li key={n} className="flex items-center gap-3 text-sm">
                  <span className="w-12 shrink-0 text-ink">{n} zvj.</span>
                  <div className="h-2.5 flex-1 overflow-hidden rounded-full bg-sand" aria-hidden="true">
                    <div className="h-full rounded-full bg-brand-600" style={{ width: `${udio}%` }} />
                  </div>
                  <span className="w-8 shrink-0 text-right tabular-nums text-ink">{broj}</span>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      <div className="flex flex-wrap gap-2" role="group" aria-label="Filter po ocjeni">
        {TABOVI.map((t) => (
          <button
            key={t.naziv}
            aria-pressed={filter === t.vrijednost}
            onClick={() => setFilter(t.vrijednost)}
            className={cn(
              "rounded-full px-4 py-1.5 text-sm font-medium transition",
              filter === t.vrijednost ? "bg-brand-600 text-white" : "bg-white text-ink hover:bg-sand",
            )}
          >
            {t.naziv}
          </button>
        ))}
      </div>

      {podaci.isLoading ? (
        <LoadingBlock />
      ) : podaci.isError ? (
        <div className="space-y-3 rounded-card bg-white p-6 shadow-card">
          <p className="text-muted">Učitavanje ocjena nije uspjelo.</p>
          <Button size="sm" onClick={() => podaci.refetch()}>
            Pokušaj ponovo
          </Button>
        </div>
      ) : !podaci.data?.stavke.length ? (
        <p className="rounded-card bg-white p-6 text-muted shadow-card">Nema ocjena.</p>
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {podaci.data.stavke.map((o) => (
              <Red key={o.id} o={o} />
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
