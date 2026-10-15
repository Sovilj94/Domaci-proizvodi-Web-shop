"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil, Plus, Power, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { PromoKodForm } from "@/components/admin/promo-kod-form";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/hooks";
import type { PromoKodInput } from "@/lib/api/contracts";
import type { PromoKod } from "@/types/domain";
import { formatPrice } from "@/lib/utils";

/** "yyyy-MM-dd" -> "31.10.2026." bez prolaska kroz vremensku zonu. */
function datum(iso: string): string {
  const [g, m, d] = iso.split("-");
  return `${d}.${m}.${g}.`;
}

function popust(p: PromoKod): string {
  return p.tip === "Procenat" ? `${p.vrijednost} %` : formatPrice(p.vrijednost);
}

function vazenje(p: PromoKod): string {
  const dijelovi: string[] = [];
  if (p.vaziOd) dijelovi.push(`od ${datum(p.vaziOd)}`);
  if (p.vaziDo) dijelovi.push(`do ${datum(p.vaziDo)}`);
  return dijelovi.length ? dijelovi.join(" ") : "bez roka";
}

function uslovi(p: PromoKod): string[] {
  const o: string[] = [];
  if (p.jednomPoKupcu) o.push("jednom po kupcu");
  if (p.samoNoviKupci) o.push("prva narudžba");
  if (p.minIznos != null && p.minIznos > 0) o.push(`min. ${formatPrice(p.minIznos)}`);
  return o;
}

function koristeno(p: PromoKod): string {
  return p.maxKoristenja != null ? `${p.brojKoristenja} / ${p.maxKoristenja}` : String(p.brojKoristenja);
}

function toInput(p: PromoKod, aktivan: boolean): PromoKodInput {
  return {
    kod: p.kod,
    opis: p.opis,
    tip: p.tip,
    vrijednost: p.vrijednost,
    minIznos: p.minIznos,
    vaziOd: p.vaziOd,
    vaziDo: p.vaziDo,
    maxKoristenja: p.maxKoristenja,
    jednomPoKupcu: p.jednomPoKupcu,
    samoNoviKupci: p.samoNoviKupci,
    aktivan,
  };
}

const ikonaDugme =
  "grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-sand hover:text-ink";

function Status({ aktivan }: { aktivan: boolean }) {
  return (
    <span
      className={
        aktivan
          ? "rounded-full bg-brand-50 px-2 py-0.5 text-xs font-medium text-brand-700"
          : "rounded-full bg-sand px-2 py-0.5 text-xs font-medium text-ink/80"
      }
    >
      {aktivan ? "Aktivan" : "Isključen"}
    </span>
  );
}

export default function AdminPromoKodoviPage() {
  const qc = useQueryClient();
  const { show } = useToast();
  const [editing, setEditing] = useState<PromoKod | null>(null);
  const [open, setOpen] = useState(false);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const kodovi = useQuery({
    queryKey: queryKeys.adminPromoKodovi(),
    queryFn: () => adminApi.promoKodovi(),
  });

  const poruka = (err: unknown, rezerva: string) =>
    err instanceof ApiRequestError ? err.message : rezerva;

  const remove = useMutation({
    mutationFn: (id: string) => adminApi.deletePromoKod(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adminPromoKodovi() });
      setConfirmId(null);
      show("Promo kod obrisan");
    },
    onError: (err) => {
      setConfirmId(null);
      show(poruka(err, "Brisanje nije uspjelo"), "error");
    },
  });

  const toggle = useMutation({
    mutationFn: (p: PromoKod) => adminApi.updatePromoKod(p.id, toInput(p, !p.aktivan)),
    onSuccess: (_d, p) => {
      qc.invalidateQueries({ queryKey: queryKeys.adminPromoKodovi() });
      show(p.aktivan ? "Promo kod isključen" : "Promo kod uključen");
    },
    onError: (err) => show(poruka(err, "Promjena nije uspjela"), "error"),
  });

  function uredi(p: PromoKod) {
    setEditing(p);
    setOpen(true);
  }

  function akcije(p: PromoKod) {
    if (confirmId === p.id) {
      return (
        <div className="flex items-center gap-2">
          <Button size="sm" variant="danger" onClick={() => remove.mutate(p.id)} disabled={remove.isPending}>
            Obriši
          </Button>
          <button onClick={() => setConfirmId(null)} className={ikonaDugme} aria-label="Otkaži">
            <X className="h-4 w-4" />
          </button>
        </div>
      );
    }
    return (
      <div className="flex items-center">
        <button onClick={() => uredi(p)} className={ikonaDugme} aria-label="Izmijeni">
          <Pencil className="h-4 w-4" />
        </button>
        <button
          onClick={() => toggle.mutate(p)}
          disabled={toggle.isPending}
          className={ikonaDugme}
          aria-label={p.aktivan ? "Isključi" : "Uključi"}
          title={p.aktivan ? "Isključi" : "Uključi"}
        >
          <Power className="h-4 w-4" />
        </button>
        {p.brojKoristenja === 0 && (
          <button
            onClick={() => setConfirmId(p.id)}
            className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
            aria-label="Obriši"
          >
            <Trash2 className="h-4 w-4" />
          </button>
        )}
      </div>
    );
  }

  const lista = kodovi.data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold">Promo kodovi</h1>
          <p className="text-muted">Popusti koje kupac upisuje na naplati.</p>
        </div>
        <Button
          onClick={() => {
            setEditing(null);
            setOpen(true);
          }}
        >
          <Plus className="h-4 w-4" /> Novi kod
        </Button>
      </div>

      {kodovi.isLoading ? (
        <LoadingBlock />
      ) : kodovi.isError ? (
        <p className="rounded-card bg-white p-6 text-red-600 shadow-card">Promo kodovi se nisu učitali.</p>
      ) : !lista.length ? (
        <p className="rounded-card bg-white p-6 text-muted shadow-card">Još nema promo kodova.</p>
      ) : (
        <>
          <div className="hidden overflow-hidden rounded-card bg-white shadow-card md:block">
            <table className="w-full text-sm">
              <thead className="border-b border-line text-left text-xs uppercase text-muted">
                <tr>
                  <th className="px-4 py-3 font-medium">Kod</th>
                  <th className="px-4 py-3 font-medium">Popust</th>
                  <th className="px-4 py-3 font-medium">Važenje</th>
                  <th className="px-4 py-3 font-medium">Uslovi</th>
                  <th className="px-4 py-3 font-medium">Korišćeno</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {lista.map((p) => (
                  <tr key={p.id}>
                    <td className="px-4 py-3">
                      <p className="font-mono font-semibold">{p.kod}</p>
                      {p.opis && <p className="text-xs text-muted">{p.opis}</p>}
                    </td>
                    <td className="px-4 py-3">{popust(p)}</td>
                    <td className="px-4 py-3">{vazenje(p)}</td>
                    <td className="px-4 py-3 text-muted">{uslovi(p).join(", ") || "—"}</td>
                    <td className="px-4 py-3">{koristeno(p)}</td>
                    <td className="px-4 py-3">
                      <Status aktivan={p.aktivan} />
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex justify-end">{akcije(p)}</div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <ul className="space-y-3 md:hidden">
            {lista.map((p) => (
              <li key={p.id} className="rounded-card bg-white p-4 shadow-card">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="font-mono font-semibold">{p.kod}</p>
                    {p.opis && <p className="text-sm text-muted">{p.opis}</p>}
                  </div>
                  <Status aktivan={p.aktivan} />
                </div>
                <p className="mt-2 text-sm">
                  <span className="font-medium">{popust(p)}</span> · {vazenje(p)} · korišćeno {koristeno(p)}
                </p>
                {uslovi(p).length > 0 && (
                  <p className="mt-1 text-xs text-muted">{uslovi(p).join(", ")}</p>
                )}
                <div className="mt-2 flex justify-end">{akcije(p)}</div>
              </li>
            ))}
          </ul>
        </>
      )}

      <PromoKodForm open={open} onClose={() => setOpen(false)} promo={editing} />
    </div>
  );
}
