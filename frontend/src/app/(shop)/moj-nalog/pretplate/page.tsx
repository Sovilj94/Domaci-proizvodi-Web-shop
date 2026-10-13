"use client";

import { useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { RefreshCw } from "lucide-react";
import { PretplataForma } from "@/components/nalog/pretplata-forma";
import { PretplataKartica } from "@/components/nalog/pretplata-kartica";
import { Button, buttonVariants } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Pretplata } from "@/types/domain";

export default function MojNalogPretplatePage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<Pretplata | null>(null);

  const pretplate = useQuery({
    queryKey: queryKeys.pretplate(),
    queryFn: () => accountApi.pretplate(),
  });

  function osvjezi(poruka: string) {
    qc.invalidateQueries({ queryKey: queryKeys.pretplate() });
    show(poruka);
  }

  function greska(err: unknown) {
    show(err instanceof ApiRequestError ? err.message : "Došlo je do greške.", "error");
  }

  const pauza = useMutation({
    mutationFn: ({ id, sedmica }: { id: string; sedmica: number }) =>
      accountApi.pauzirajPretplatu(id, sedmica),
    onSuccess: () => osvjezi("Pretplata je pauzirana."),
    onError: greska,
  });

  const nastavak = useMutation({
    mutationFn: (id: string) => accountApi.nastaviPretplatu(id),
    onSuccess: () => osvjezi("Pretplata je ponovo aktivna."),
    onError: greska,
  });

  const otkaz = useMutation({
    mutationFn: (id: string) => accountApi.otkaziPretplatu(id),
    onSuccess: () => osvjezi("Pretplata je otkazana."),
    onError: greska,
  });

  const lista = pretplate.data ?? [];

  function otvoriIzmjenu(p: Pretplata) {
    setEditing(p);
    setFormOpen(true);
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Pretplate</h1>
        {lista.length > 0 && (
          <Link href="/korpice" className={buttonVariants({ variant: "secondary", size: "sm" })}>
            Pogledaj korpice
          </Link>
        )}
      </div>

      <div className="rounded-card border border-line bg-surface p-5 shadow-card">
        <ol className="list-decimal space-y-1 pl-5 text-sm text-ink">
          <li>Korpica stiže svake sedmice, bez naručivanja iznova.</li>
          <li>Pauziraj kad te nema — preskaču se samo isporuke koje odabereš.</li>
          <li>Prvih pet isporuka po punoj cijeni; od šeste je niža.</li>
        </ol>
        <p className="mt-3 text-sm text-muted">
          Plaćanje ostaje pouzećem, kao i za svaku drugu narudžbu. Narudžba se pravi dva dana
          prije isporuke, pa pauzu i otkaz uradi prije toga.
        </p>
      </div>

      {pretplate.isLoading ? (
        <LoadingBlock />
      ) : pretplate.isError ? (
        /* Bez ove grane neuspjelo učitavanje padne u prazno stanje ispod, pa kupac sa
           aktivnom pretplatom pročita „Nemaš nijednu pretplatu" — izgleda kao da mu je
           pretplata nestala. Isti razlog kao na ekranu adresa. */
        <EmptyState
          title="Ne mogu da učitam pretplate"
          description="Provjeri konekciju pa pokušaj ponovo."
          icon={<RefreshCw className="h-7 w-7" />}
          action={<Button onClick={() => pretplate.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : lista.length === 0 ? (
        <EmptyState
          title="Nemaš nijednu pretplatu"
          description="Izaberi korpicu i stizaće ti svake sedmice, bez naručivanja iznova."
          icon={<RefreshCw className="h-7 w-7" />}
          action={
            <Link href="/korpice" className={buttonVariants()}>
              Izaberi korpicu
            </Link>
          }
        />
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {lista.map((p) => (
            <PretplataKartica
              key={p.id}
              pretplata={p}
              onIzmijeni={() => otvoriIzmjenu(p)}
              onPauziraj={(sedmica) => pauza.mutate({ id: p.id, sedmica })}
              onNastavi={() => nastavak.mutate(p.id)}
              onOtkazi={() => otkaz.mutateAsync(p.id)}
              pending={
                (pauza.isPending && pauza.variables?.id === p.id) ||
                (nastavak.isPending && nastavak.variables === p.id) ||
                (otkaz.isPending && otkaz.variables === p.id)
              }
            />
          ))}
        </div>
      )}

      <PretplataForma
        open={formOpen}
        onClose={() => setFormOpen(false)}
        pretplata={editing}
      />
    </div>
  );
}
