"use client";

import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, Trash2, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi, korpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/hooks";
import type { ReceptInput } from "@/lib/api/contracts";
import { TEZINA_TEKST, type ReceptAdmin, type TezinaRecepta } from "@/types/domain";

interface SastojakRed {
  kolicina: string;
  naziv: string;
  productId: string;
}

interface FormState {
  naslov: string;
  opis: string;
  slikaUrl: string;
  vrijemeMinuta: string;
  porcije: string;
  tezina: TezinaRecepta;
  koraci: string;
  objavljen: boolean;
  sastojci: SastojakRed[];
}

const PRAZNO: FormState = {
  naslov: "",
  opis: "",
  slikaUrl: "",
  vrijemeMinuta: "30",
  porcije: "4",
  tezina: "Lako",
  koraci: "",
  objavljen: false,
  sastojci: [],
};

export function ReceptForm({
  open,
  onClose,
  recept,
}: {
  open: boolean;
  onClose: () => void;
  recept?: ReceptAdmin | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState<FormState>(PRAZNO);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  const products = useQuery({
    queryKey: ["admin", "products"],
    queryFn: () => adminApi.products(),
    enabled: open,
  });
  const korpice = useQuery({
    queryKey: queryKeys.korpiceLista(),
    queryFn: () => korpiceApi.lista(),
    enabled: open,
  });

  // Samo obični proizvodi (korpice su također Product, ali ne ulaze u recept), po imenu.
  const ponuda = useMemo(() => {
    const korpiceIds = new Set((korpice.data ?? []).map((k) => k.id));
    return [...(products.data ?? [])]
      .filter((p) => !korpiceIds.has(p.id))
      .sort((a, b) => a.name.localeCompare(b.name, "sr"));
  }, [products.data, korpice.data]);

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      recept
        ? {
            naslov: recept.naslov,
            opis: recept.opis,
            slikaUrl: recept.slikaUrl ?? "",
            vrijemeMinuta: String(recept.vrijemeMinuta),
            porcije: String(recept.porcije),
            tezina: recept.tezina,
            koraci: recept.koraci.join("\n"),
            objavljen: recept.objavljen,
            sastojci: recept.sastojci.map((s) => ({
              kolicina: s.kolicina,
              naziv: s.naziv,
              productId: s.productId ?? "",
            })),
          }
        : PRAZNO,
    );
  }, [open, recept]);

  const mutation = useMutation({
    mutationFn: (body: ReceptInput) =>
      recept ? adminApi.updateRecept(recept.id, body) : adminApi.createRecept(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adminRecepti() });
      qc.invalidateQueries({ queryKey: queryKeys.recepti() });
      qc.invalidateQueries({ queryKey: ["recepti"] });
      show(recept ? "Recept ažuriran" : "Recept dodan");
      onClose();
    },
    onError: (err) => {
      // Greška vezana za polje ostaje ispod polja; sve ostalo mora dati toast.
      if (err instanceof ApiRequestError) {
        const poljaGreske = err.fieldErrors;
        setErrors(poljaGreske);
        if (Object.keys(poljaGreske).length > 0) return;
        show(err.message, "error");
        return;
      }
      show("Čuvanje nije uspjelo", "error");
    },
  });

  function setSastojak(i: number, patch: Partial<SastojakRed>) {
    setForm((f) => ({
      ...f,
      sastojci: f.sastojci.map((s, idx) => (idx === i ? { ...s, ...patch } : s)),
    }));
  }

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    mutation.mutate({
      naslov: form.naslov,
      opis: form.opis || undefined,
      slikaUrl: form.slikaUrl || undefined,
      vrijemeMinuta: Math.trunc(Number(form.vrijemeMinuta)) || 0,
      porcije: Math.trunc(Number(form.porcije)) || 0,
      tezina: form.tezina,
      koraci: form.koraci,
      objavljen: form.objavljen,
      sastojci: form.sastojci.map((s) => ({
        naziv: s.naziv,
        kolicina: s.kolicina || undefined,
        productId: s.productId || undefined,
      })),
    });
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50">
      <button aria-label="Zatvori" onClick={onClose} className="absolute inset-0 bg-black/40" />
      <div className="absolute right-0 top-0 flex h-full w-full max-w-xl flex-col bg-cream shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-6 py-4">
          <h2 className="font-display text-lg font-bold">{recept ? "Uredi recept" : "Novi recept"}</h2>
          <button onClick={onClose} className="rounded-full p-1.5 hover:bg-sand" aria-label="Zatvori">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
          <Field label="Naslov" required error={errors["input.naslov"]?.[0]}>
            <Input
              value={form.naslov}
              onChange={(e) => setForm({ ...form, naslov: e.target.value })}
              maxLength={120}
            />
          </Field>

          <Field label="Opis" error={errors["input.opis"]?.[0]}>
            <Textarea
              value={form.opis}
              onChange={(e) => setForm({ ...form, opis: e.target.value })}
              rows={3}
              maxLength={500}
            />
          </Field>

          <Field label="URL slike" error={errors["input.slikaUrl"]?.[0]}>
            <Input
              value={form.slikaUrl}
              onChange={(e) => setForm({ ...form, slikaUrl: e.target.value })}
              maxLength={500}
            />
          </Field>

          <div className="grid grid-cols-3 gap-3">
            <Field label="Vrijeme (min)" required error={errors["input.vrijemeMinuta"]?.[0]}>
              <Input
                type="number"
                min={1}
                max={600}
                value={form.vrijemeMinuta}
                onChange={(e) => setForm({ ...form, vrijemeMinuta: e.target.value })}
              />
            </Field>
            <Field label="Porcije" required error={errors["input.porcije"]?.[0]}>
              <Input
                type="number"
                min={1}
                max={20}
                value={form.porcije}
                onChange={(e) => setForm({ ...form, porcije: e.target.value })}
              />
            </Field>
            <Field label="Težina" required error={errors["input.tezina"]?.[0]}>
              <Select
                value={form.tezina}
                onChange={(e) => setForm({ ...form, tezina: e.target.value as TezinaRecepta })}
              >
                {(Object.keys(TEZINA_TEKST) as TezinaRecepta[]).map((t) => (
                  <option key={t} value={t}>
                    {TEZINA_TEKST[t]}
                  </option>
                ))}
              </Select>
            </Field>
          </div>

          <Field label="Koraci" required error={errors["input.koraci"]?.[0]}>
            <Textarea
              value={form.koraci}
              onChange={(e) => setForm({ ...form, koraci: e.target.value })}
              rows={10}
              maxLength={8000}
            />
            <p className="mt-1 text-xs text-muted">Svaki korak u novom redu.</p>
          </Field>

          <fieldset className="space-y-3">
            <legend className="mb-1.5 text-sm font-medium text-ink">Sastojci</legend>
            {errors["input.sastojci"]?.[0] && (
              <p className="text-xs font-medium text-red-600">{errors["input.sastojci"][0]}</p>
            )}
            {form.sastojci.map((s, i) => {
              const greske = [
                errors[`input.sastojci[${i}].naziv`]?.[0],
                errors[`input.sastojci[${i}].kolicina`]?.[0],
              ].filter(Boolean);
              return (
                <div key={i} className="rounded-xl bg-white p-3 shadow-card">
                  <div className="flex items-start gap-2">
                    <div className="grid flex-1 grid-cols-[6rem_1fr] gap-2">
                      <Input
                        aria-label={`Količina, sastojak ${i + 1}`}
                        placeholder="Količina"
                        value={s.kolicina}
                        onChange={(e) => setSastojak(i, { kolicina: e.target.value })}
                        maxLength={40}
                      />
                      <Input
                        aria-label={`Naziv, sastojak ${i + 1}`}
                        placeholder="Naziv"
                        value={s.naziv}
                        onChange={(e) => setSastojak(i, { naziv: e.target.value })}
                        maxLength={100}
                      />
                      <div className="col-span-2">
                        <Select
                          aria-label={`Proizvod iz ponude, sastojak ${i + 1}`}
                          value={s.productId}
                          onChange={(e) => setSastojak(i, { productId: e.target.value })}
                        >
                          <option value="">— bez veze —</option>
                          {ponuda.map((p) => (
                            <option key={p.id} value={p.id}>
                              {p.name}
                            </option>
                          ))}
                        </Select>
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() =>
                        setForm((f) => ({ ...f, sastojci: f.sastojci.filter((_, idx) => idx !== i) }))
                      }
                      className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-muted transition hover:bg-red-50 hover:text-red-600"
                      aria-label="Ukloni sastojak"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                  {greske.map((g) => (
                    <p key={g} className="mt-1 text-xs font-medium text-red-600">
                      {g}
                    </p>
                  ))}
                </div>
              );
            })}
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={() =>
                setForm((f) => ({ ...f, sastojci: [...f.sastojci, { kolicina: "", naziv: "", productId: "" }] }))
              }
            >
              <Plus className="h-4 w-4" /> Dodaj sastojak
            </Button>
          </fieldset>

          <label className="flex items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              checked={form.objavljen}
              onChange={(e) => setForm({ ...form, objavljen: e.target.checked })}
              className="h-4 w-4 accent-brand-600"
            />
            Objavljen
          </label>

          <Button type="submit" className="w-full" disabled={mutation.isPending}>
            {mutation.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        </form>
      </div>
    </div>
  );
}
