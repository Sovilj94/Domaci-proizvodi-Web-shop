"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { PLAN_TEKST } from "@/lib/planovi";
import { cn, formatPrice } from "@/lib/utils";

const TABOVI = [
  { vrijednost: undefined, naziv: "Sve" },
  { vrijednost: "Aktivna", naziv: "Aktivne" },
  { vrijednost: "Pauzirana", naziv: "Pauzirane" },
  { vrijednost: "Otkazana", naziv: "Otkazane" },
];

/**
 * Pretplate za nabavku i podršku (R2). Brojevi za narednu sedmicu i lista; „Otkaži" je put
 * kojim podrška otkazuje pretplatu kupcu čija obaveza plana još traje (kupac to sam ne može).
 */
export default function AdminPretplatePage() {
  const [status, setStatus] = useState<string | undefined>(undefined);
  const [potvrda, setPotvrda] = useState<string | null>(null);
  const qc = useQueryClient();
  const { show } = useToast();
  const q = useQuery({ queryKey: ["admin", "pretplate", status], queryFn: () => adminApi.pretplate(status) });

  const otkaz = useMutation({
    mutationFn: (id: string) => adminApi.otkaziPretplatu(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "pretplate"] });
      setPotvrda(null);
      show("Pretplata je otkazana");
    },
    onError: (err) => show(err instanceof ApiRequestError ? err.message : "Otkaz nije uspio", "error"),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Pretplate</h1>
        {q.data && (
          <p className="text-sm text-muted">
            Za {q.data.sedmica}. sedmicu: {q.data.brojAktivnih} aktivnih
            {q.data.cekajuSadrzaj > 0 && (
              <span className="font-medium text-red-700"> · {q.data.cekajuSadrzaj} čeka objavu sadržaja</span>
            )}
          </p>
        )}
      </div>

      <div className="flex flex-wrap gap-2">
        {TABOVI.map((t) => (
          <button
            key={t.naziv}
            onClick={() => setStatus(t.vrijednost)}
            className={cn(
              "rounded-full px-4 py-1.5 text-sm font-medium",
              status === t.vrijednost ? "bg-brand-600 text-white" : "bg-surface text-ink ring-1 ring-line",
            )}
          >
            {t.naziv}
          </button>
        ))}
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError ? (
        <p className="text-red-700">Lista nije učitana.</p>
      ) : !q.data?.stavke.length ? (
        <p className="text-muted">Nema pretplata.</p>
      ) : (
        <ul className="divide-y divide-line rounded-card border border-line bg-surface">
          {q.data.stavke.map((p) => (
            <li key={p.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm">
              <div className="min-w-0">
                <p className="font-medium">
                  {p.kupac} · {p.korpica}
                </p>
                <p className="text-muted">
                  {PLAN_TEKST[p.plan]} · {formatPrice(p.cijena)} · {p.brojIsporuka} isporuka
                  {p.preostaloObaveze > 0 && ` · obaveza još ${p.preostaloObaveze}`} · {p.status}
                  {!p.sadrzajSpreman && p.status === "Aktivna" && " · sadržaj nije objavljen"}
                </p>
                <p className="text-muted">
                  {p.telefon} {p.email && `· ${p.email}`} · {p.adresa}
                </p>
              </div>
              {p.status !== "Otkazana" &&
                (potvrda === p.id ? (
                  <div className="flex gap-2">
                    <Button size="sm" variant="danger" disabled={otkaz.isPending} onClick={() => otkaz.mutate(p.id)}>
                      Potvrdi otkaz
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setPotvrda(null)}>
                      Odustani
                    </Button>
                  </div>
                ) : (
                  <Button size="sm" variant="secondary" onClick={() => setPotvrda(p.id)}>
                    Otkaži
                  </Button>
                ))}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
