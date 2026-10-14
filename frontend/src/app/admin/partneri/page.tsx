"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Select, Textarea } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { queryKeys } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";
import { STATUS_PRIJAVE_TEKST, type PartnerPrijava, type StatusPrijave } from "@/types/domain";

const TABOVI: { vrijednost?: StatusPrijave; naziv: string }[] = [
  { naziv: "Sve" },
  { vrijednost: "Nova", naziv: "Nove" },
  { vrijednost: "UObradi", naziv: "U obradi" },
  { vrijednost: "Prihvacena", naziv: "Prihvaćene" },
  { vrijednost: "Odbijena", naziv: "Odbijene" },
];

/* Svi parovi daju AA kontrast: tamni tekst na svijetloj podlozi. */
const BEDZ: Record<StatusPrijave, string> = {
  Nova: "bg-honey-100 text-ink",
  UObradi: "bg-sand text-ink",
  Prihvacena: "bg-brand-50 text-brand-700",
  Odbijena: "bg-red-50 text-red-800",
};

function datum(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "" : d.toLocaleDateString("sr-Latn-BA");
}

function Red({ p }: { p: PartnerPrijava }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [status, setStatus] = useState<StatusPrijave>(p.status);
  const [napomena, setNapomena] = useState(p.adminNapomena);

  const save = useMutation({
    // "" briše napomenu, što je baš ono što admin vidi u praznom polju.
    mutationFn: () => adminApi.azurirajPrijavu(p.id, { status, adminNapomena: napomena }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "partneri"] });
      show("Prijava sačuvana");
    },
    onError: (err) =>
      show(err instanceof ApiRequestError ? err.message : "Čuvanje nije uspjelo", "error"),
  });

  const promijenjeno = status !== p.status || napomena !== p.adminNapomena;

  return (
    <li className="space-y-3 p-4">
      <div className="flex flex-wrap items-center gap-2">
        <p className="font-medium">{p.ime}</p>
        <span className={cn("rounded-full px-2 py-0.5 text-xs font-medium", BEDZ[p.status])}>
          {STATUS_PRIJAVE_TEKST[p.status]}
        </span>
        <span className="text-xs text-muted">{datum(p.kreirana)}</span>
      </div>
      <p className="text-sm text-muted">
        {p.gazdinstvo && `${p.gazdinstvo} · `}
        {p.mjesto} ·{" "}
        <a
          href={`tel:${p.telefon.replace(/[^\d+]/g, "")}`}
          className="font-medium text-brand-700 underline"
        >
          {p.telefon}
        </a>
        {p.email && (
          <>
            {" · "}
            <a href={`mailto:${p.email}`} className="font-medium text-brand-700 underline">
              {p.email}
            </a>
          </>
        )}
      </p>
      <p className="whitespace-pre-line text-sm text-ink">{p.proizvodi}</p>
      <div className="grid gap-2 sm:grid-cols-[12rem_1fr_auto] sm:items-start">
        <Select
          aria-label="Status prijave"
          value={status}
          onChange={(e) => setStatus(e.target.value as StatusPrijave)}
        >
          {(Object.keys(STATUS_PRIJAVE_TEKST) as StatusPrijave[]).map((s) => (
            <option key={s} value={s}>
              {STATUS_PRIJAVE_TEKST[s]}
            </option>
          ))}
        </Select>
        <Textarea
          aria-label="Napomena"
          placeholder="Napomena (vidi samo admin)"
          className="min-h-11"
          rows={2}
          value={napomena}
          onChange={(e) => setNapomena(e.target.value)}
        />
        <Button onClick={() => save.mutate()} disabled={save.isPending || !promijenjeno}>
          {save.isPending ? "Čuvam…" : "Sačuvaj"}
        </Button>
      </div>
    </li>
  );
}

export default function AdminPartneriPage() {
  const [filter, setFilter] = useState<StatusPrijave | undefined>(undefined);
  const lista = useQuery({
    queryKey: queryKeys.adminPartneri(filter),
    queryFn: () => adminApi.partneri(filter),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold">Partneri</h1>
        <p className="text-muted">Prijave sa stranice „Postani partner“.</p>
      </div>

      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Status prijave">
        {TABOVI.map((t) => (
          <button
            key={t.naziv}
            role="tab"
            aria-selected={filter === t.vrijednost}
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

      {lista.isLoading ? (
        <LoadingBlock />
      ) : !lista.data?.length ? (
        <p className="rounded-card bg-white p-6 text-muted shadow-card">Nema prijava.</p>
      ) : (
        <div className="overflow-hidden rounded-card bg-white shadow-card">
          <ul className="divide-y divide-line">
            {lista.data.map((p) => (
              <Red key={`${p.id}-${p.status}-${p.adminNapomena}`} p={p} />
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
