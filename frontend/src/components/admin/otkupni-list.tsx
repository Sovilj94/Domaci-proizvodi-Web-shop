"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { OtkupniList } from "@/lib/api/contracts";
import { formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(3)).toLocaleString("sr-Latn");
/** Prihvata i decimalni zarez („2,5"). Prazno = nije uneseno. */
const procitaj = (s: string): number | undefined => {
  const t = s.trim().replace(",", ".");
  if (!t) return undefined;
  const n = Number(t);
  return Number.isFinite(n) ? n : undefined;
};

/**
 * Unos stvarno primljenih količina sa potpisanog otkupnog lista. Samo evidencija: ne mijenja
 * stanje ni narudžbe kupaca. Razlika prema naručenom se vidi odmah.
 */
export function OtkupniListUnos({
  list,
  kljuc,
  onGotovo,
}: {
  list: OtkupniList;
  kljuc: readonly unknown[];
  onGotovo: () => void;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [vrijednosti, setVrijednosti] = useState<Record<string, string>>(() =>
    Object.fromEntries(list.stavke.map((s) => [s.id, s.primljeno != null ? String(s.primljeno) : ""])),
  );

  const sacuvaj = useMutation({
    mutationFn: () =>
      adminApi.unesiPrimljeno(list.id, {
        stavke: list.stavke.map((s) => ({ stavkaId: s.id, primljeno: procitaj(vrijednosti[s.id] ?? "") })),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kljuc });
      show("Primljene količine su sačuvane");
      onGotovo();
    },
    onError: (e) => show(e instanceof ApiRequestError ? e.message : "Čuvanje nije uspjelo", "error"),
  });

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        sacuvaj.mutate();
      }}
      className="space-y-3 p-3"
    >
      <table className="w-full text-sm">
        <thead className="text-left text-xs text-muted">
          <tr className="border-b border-line">
            <th className="py-2 pr-2 font-medium">Artikal</th>
            <th className="px-2 py-2 text-right font-medium">Naručeno</th>
            <th className="px-2 py-2 text-right font-medium">Primljeno</th>
            <th className="py-2 pl-2 text-right font-medium">Razlika</th>
          </tr>
        </thead>
        <tbody className="tabular-nums">
          {list.stavke.map((s) => {
            const p = procitaj(vrijednosti[s.id] ?? "");
            const razlika = p !== undefined ? p - s.naruceno : null;
            return (
              <tr key={s.id} className="border-b border-line last:border-0">
                <td className="py-2 pr-2 font-medium text-ink">{s.naziv}</td>
                <td className="whitespace-nowrap px-2 py-2 text-right">
                  {broj(s.naruceno)} {s.jedinica}
                </td>
                <td className="px-2 py-2 text-right">
                  <Input
                    aria-label={`Primljeno: ${s.naziv}`}
                    inputMode="decimal"
                    className="ml-auto w-24 text-right"
                    value={vrijednosti[s.id] ?? ""}
                    onChange={(e) => setVrijednosti((v) => ({ ...v, [s.id]: e.target.value }))}
                  />
                </td>
                <td
                  className={
                    razlika === null || Math.abs(razlika) < 0.0005
                      ? "py-2 pl-2 text-right text-muted"
                      : "py-2 pl-2 text-right font-semibold text-red-600"
                  }
                >
                  {razlika === null ? "—" : `${razlika > 0 ? "+" : ""}${broj(razlika)}`}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-muted">
          {list.iznosPrimljeno != null ? `Po primljenom: ${formatPrice(list.iznosPrimljeno)}` : "Primljeno još nije uneseno."}
        </p>
        <div className="flex gap-2">
          <Button type="button" size="sm" variant="secondary" onClick={onGotovo}>
            Otkaži
          </Button>
          <Button type="submit" size="sm" disabled={sacuvaj.isPending}>
            {sacuvaj.isPending ? "Čuvam…" : "Sačuvaj primljeno"}
          </Button>
        </div>
      </div>
    </form>
  );
}
