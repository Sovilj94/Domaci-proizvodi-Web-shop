"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/hooks";
import type { PromoKodInput } from "@/lib/api/contracts";
import type { PromoKod, PromoTip } from "@/types/domain";

interface FormState {
  kod: string;
  opis: string;
  tip: PromoTip;
  vrijednost: string;
  minIznos: string;
  vaziOd: string;
  vaziDo: string;
  maxKoristenja: string;
  jednomPoKupcu: boolean;
  samoNoviKupci: boolean;
  aktivan: boolean;
}

const PRAZNO: FormState = {
  kod: "",
  opis: "",
  tip: "Procenat",
  vrijednost: "",
  minIznos: "",
  vaziOd: "",
  vaziDo: "",
  maxKoristenja: "",
  jednomPoKupcu: false,
  samoNoviKupci: false,
  aktivan: true,
};

/** Prazno je null; "5,5" i "5.5" su oba ispravan unos. */
function broj(v: string): number | null {
  const t = v.trim().replace(",", ".");
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/** Ključ greške dolazi kao "input.kod" ili "Input.Tip", pa se poredi bez obzira na velika slova. */
function greska(errors: Record<string, string[]>, polje: string): string | undefined {
  const cilj = `input.${polje}`.toLowerCase();
  for (const [k, v] of Object.entries(errors)) {
    if (k.toLowerCase() === cilj) return v[0];
  }
  return undefined;
}

export function PromoKodForm({
  open,
  onClose,
  promo,
}: {
  open: boolean;
  onClose: () => void;
  promo?: PromoKod | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState<FormState>(PRAZNO);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      promo
        ? {
            kod: promo.kod,
            opis: promo.opis,
            tip: promo.tip,
            vrijednost: String(promo.vrijednost),
            minIznos: promo.minIznos == null ? "" : String(promo.minIznos),
            vaziOd: promo.vaziOd ?? "",
            vaziDo: promo.vaziDo ?? "",
            maxKoristenja: promo.maxKoristenja == null ? "" : String(promo.maxKoristenja),
            jednomPoKupcu: promo.jednomPoKupcu,
            samoNoviKupci: promo.samoNoviKupci,
            aktivan: promo.aktivan,
          }
        : PRAZNO,
    );
  }, [open, promo]);

  const mutation = useMutation({
    mutationFn: (body: PromoKodInput) =>
      promo ? adminApi.updatePromoKod(promo.id, body) : adminApi.createPromoKod(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adminPromoKodovi() });
      show(promo ? "Promo kod ažuriran" : "Promo kod dodan");
      onClose();
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) {
        if (err.apiError.code === "promo_kod_postoji") {
          setErrors({ "input.kod": ["Promo kod sa tim imenom već postoji."] });
          return;
        }
        const poljaGreske = err.fieldErrors;
        setErrors(poljaGreske);
        if (Object.keys(poljaGreske).length > 0) return;
        show(err.message, "error");
        return;
      }
      show("Čuvanje nije uspjelo", "error");
    },
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    const limit = broj(form.maxKoristenja);
    mutation.mutate({
      kod: form.kod.trim(),
      opis: form.opis.trim(),
      tip: form.tip,
      vrijednost: broj(form.vrijednost) ?? 0,
      minIznos: broj(form.minIznos),
      vaziOd: form.vaziOd || null,
      vaziDo: form.vaziDo || null,
      maxKoristenja: limit == null ? null : Math.trunc(limit),
      jednomPoKupcu: form.jednomPoKupcu,
      samoNoviKupci: form.samoNoviKupci,
      aktivan: form.aktivan,
    });
  }

  if (!open) return null;

  const labelKlasa = "flex items-center gap-2 text-sm text-ink";
  const tipGreska = greska(errors, "tip");

  return (
    <div className="fixed inset-0 z-50">
      <button aria-label="Zatvori" onClick={onClose} className="absolute inset-0 bg-black/40" />
      <div className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-cream shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-6 py-4">
          <h2 className="font-display text-lg font-bold">{promo ? "Uredi promo kod" : "Novi promo kod"}</h2>
          <button onClick={onClose} className="rounded-full p-1.5 hover:bg-sand" aria-label="Zatvori">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
          <Field label="Kod" required error={greska(errors, "kod")}>
            <Input
              value={form.kod}
              onChange={(e) => setForm({ ...form, kod: e.target.value.toUpperCase() })}
              placeholder="npr. JESEN10"
              maxLength={30}
              disabled={!!promo}
              className="font-mono"
            />
            {promo && <p className="mt-1 text-xs text-muted">Kod se ne mijenja poslije kreiranja</p>}
          </Field>

          <Field label="Opis" error={greska(errors, "opis")}>
            <Input
              value={form.opis}
              onChange={(e) => setForm({ ...form, opis: e.target.value })}
              maxLength={200}
            />
          </Field>

          <fieldset>
            <legend className="mb-1.5 text-sm font-medium text-ink">Vrsta popusta</legend>
            <div className="flex gap-6">
              {(["Procenat", "Iznos"] as const).map((t) => (
                <label key={t} className={labelKlasa}>
                  <input
                    type="radio"
                    name="tip"
                    checked={form.tip === t}
                    onChange={() => setForm({ ...form, tip: t })}
                    className="h-4 w-4 accent-brand-600"
                  />
                  {t === "Procenat" ? "Procenat" : "Iznos u KM"}
                </label>
              ))}
            </div>
            {tipGreska && <p className="mt-1 text-xs text-red-600">{tipGreska}</p>}
          </fieldset>

          <Field
            label={form.tip === "Procenat" ? "Procenat popusta (1–90)" : "Iznos popusta (KM)"}
            required
            error={greska(errors, "vrijednost")}
          >
            <Input
              type="number"
              inputMode="decimal"
              min={0}
              step="0.01"
              value={form.vrijednost}
              onChange={(e) => setForm({ ...form, vrijednost: e.target.value })}
            />
          </Field>

          <Field label="Minimalan iznos robe (KM)" error={greska(errors, "minIznos")}>
            <Input
              type="number"
              inputMode="decimal"
              min={0}
              step="0.01"
              value={form.minIznos}
              onChange={(e) => setForm({ ...form, minIznos: e.target.value })}
              placeholder="bez minimuma"
            />
          </Field>

          <div className="grid grid-cols-2 gap-4">
            <Field label="Važi od" error={greska(errors, "vaziOd")}>
              <Input
                type="date"
                value={form.vaziOd}
                onChange={(e) => setForm({ ...form, vaziOd: e.target.value })}
              />
            </Field>
            <Field label="Važi do" error={greska(errors, "vaziDo")}>
              <Input
                type="date"
                value={form.vaziDo}
                onChange={(e) => setForm({ ...form, vaziDo: e.target.value })}
              />
            </Field>
          </div>
          <p className="-mt-2 text-xs text-muted">Prazno polje znači bez granice.</p>

          <Field label="Limit korišćenja" error={greska(errors, "maxKoristenja")}>
            <Input
              type="number"
              min={1}
              step={1}
              value={form.maxKoristenja}
              onChange={(e) => setForm({ ...form, maxKoristenja: e.target.value })}
              placeholder="bez limita"
            />
          </Field>

          <div className="space-y-2">
            <label className={labelKlasa}>
              <input
                type="checkbox"
                checked={form.jednomPoKupcu}
                onChange={(e) => setForm({ ...form, jednomPoKupcu: e.target.checked })}
                className="h-4 w-4 accent-brand-600"
              />
              Jednom po kupcu
            </label>
            <label className={labelKlasa}>
              <input
                type="checkbox"
                checked={form.samoNoviKupci}
                onChange={(e) => setForm({ ...form, samoNoviKupci: e.target.checked })}
                className="h-4 w-4 accent-brand-600"
              />
              Samo za prvu narudžbu
            </label>
            <label className={labelKlasa}>
              <input
                type="checkbox"
                checked={form.aktivan}
                onChange={(e) => setForm({ ...form, aktivan: e.target.checked })}
                className="h-4 w-4 accent-brand-600"
              />
              Aktivan
            </label>
          </div>

          <Button type="submit" className="w-full" disabled={mutation.isPending}>
            {mutation.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        </form>
      </div>
    </div>
  );
}
