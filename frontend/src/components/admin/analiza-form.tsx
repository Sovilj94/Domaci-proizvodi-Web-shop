"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys, useProducers, useProducts } from "@/lib/api/hooks";
import type { AnalizaInput } from "@/lib/api/contracts";
import { REZULTAT_ANALIZE_TEKST, type Analiza, type RezultatAnalize } from "@/types/domain";

const PRAZNO = {
  producerId: "",
  productId: "",
  uzorak: "",
  laboratorija: "",
  brojIzvjestaja: "",
  datumUzorkovanja: "",
  ispitano: "",
  rezultat: "Uskladjeno" as RezultatAnalize,
  napomena: "",
  pdfUrl: "",
  objavljena: true,
};

export function AnalizaForm({
  open,
  onClose,
  analiza,
}: {
  open: boolean;
  onClose: () => void;
  analiza?: Analiza | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState(PRAZNO);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  const producers = useProducers();
  // Javni katalog: već izostavlja korpice, a vezati analizu za korpicu nema smisla.
  const products = useProducts({ producer: form.producerId || undefined });

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      analiza
        ? {
            producerId: analiza.producerId,
            productId: analiza.productId ?? "",
            uzorak: analiza.uzorak,
            laboratorija: analiza.laboratorija,
            brojIzvjestaja: analiza.brojIzvjestaja,
            datumUzorkovanja: analiza.datumUzorkovanja,
            ispitano: analiza.ispitano,
            rezultat: analiza.rezultat,
            napomena: analiza.napomena,
            pdfUrl: analiza.pdfUrl ?? "",
            objavljena: analiza.objavljena,
          }
        : PRAZNO,
    );
  }, [open, analiza]);

  const mutation = useMutation({
    mutationFn: (body: AnalizaInput) =>
      analiza ? adminApi.updateAnaliza(analiza.id, body) : adminApi.createAnaliza(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adminAnalize() });
      qc.invalidateQueries({ queryKey: ["analize"] });
      show(analiza ? "Analiza ažurirana" : "Analiza dodana");
      onClose();
    },
    onError: (err) => {
      // Greška vezana za polje ostaje ispod polja; sve ostalo mora dati povratnu informaciju.
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

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    mutation.mutate({
      producerId: form.producerId,
      productId: form.productId || undefined,
      uzorak: form.uzorak,
      laboratorija: form.laboratorija,
      brojIzvjestaja: form.brojIzvjestaja || undefined,
      datumUzorkovanja: form.datumUzorkovanja,
      ispitano: form.ispitano,
      rezultat: form.rezultat,
      napomena: form.napomena || undefined,
      pdfUrl: form.pdfUrl || undefined,
      objavljena: form.objavljena,
    });
  }

  if (!open) return null;

  const productOptions = products.data ?? [];
  // Uređivanje: vezani proizvod može biti nedostupan u katalogu; ne gubi ga tiho.
  const nedostajeVezani =
    !!form.productId && !productOptions.some((p) => p.id === form.productId);

  return (
    <div className="fixed inset-0 z-50">
      <button aria-label="Zatvori" onClick={onClose} className="absolute inset-0 bg-black/40" />
      <div className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-cream shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-6 py-4">
          <h2 className="font-display text-lg font-bold">{analiza ? "Uredi analizu" : "Nova analiza"}</h2>
          <button onClick={onClose} className="rounded-full p-1.5 hover:bg-sand" aria-label="Zatvori">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
          <Field label="Proizvođač" required error={errors["input.producerId"]?.[0]}>
            <Select
              value={form.producerId}
              onChange={(e) => setForm({ ...form, producerId: e.target.value, productId: "" })}
              required
            >
              <option value="">— izaberi —</option>
              {producers.data?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name} ({p.village})
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Proizvod" error={errors["input.productId"]?.[0]}>
            <Select
              value={form.productId}
              onChange={(e) => setForm({ ...form, productId: e.target.value })}
              disabled={!form.producerId}
            >
              <option value="">— bez veze —</option>
              {nedostajeVezani && <option value={form.productId}>{analiza?.proizvod ?? "Vezani proizvod"}</option>}
              {productOptions.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Uzorak" required error={errors["input.uzorak"]?.[0]}>
            <Input
              value={form.uzorak}
              onChange={(e) => setForm({ ...form, uzorak: e.target.value })}
              placeholder="npr. Paradajz, staklenik 2"
              maxLength={120}
            />
          </Field>

          <Field label="Laboratorija" required error={errors["input.laboratorija"]?.[0]}>
            <Input
              value={form.laboratorija}
              onChange={(e) => setForm({ ...form, laboratorija: e.target.value })}
              maxLength={120}
            />
          </Field>

          <Field label="Broj izvještaja" error={errors["input.brojIzvjestaja"]?.[0]}>
            <Input
              value={form.brojIzvjestaja}
              onChange={(e) => setForm({ ...form, brojIzvjestaja: e.target.value })}
              maxLength={60}
            />
          </Field>

          <Field label="Datum uzorkovanja" required error={errors["input.datumUzorkovanja"]?.[0]}>
            <Input
              type="date"
              value={form.datumUzorkovanja}
              onChange={(e) => setForm({ ...form, datumUzorkovanja: e.target.value })}
            />
          </Field>

          <Field label="Ispitano" required error={errors["input.ispitano"]?.[0]}>
            <Input
              value={form.ispitano}
              onChange={(e) => setForm({ ...form, ispitano: e.target.value })}
              placeholder="npr. ostaci pesticida, teški metali"
              maxLength={200}
            />
          </Field>

          <Field label="Rezultat" required error={errors["input.rezultat"]?.[0]}>
            <Select
              value={form.rezultat}
              onChange={(e) => setForm({ ...form, rezultat: e.target.value as RezultatAnalize })}
            >
              {(Object.keys(REZULTAT_ANALIZE_TEKST) as RezultatAnalize[]).map((r) => (
                <option key={r} value={r}>
                  {REZULTAT_ANALIZE_TEKST[r]}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Napomena" error={errors["input.napomena"]?.[0]}>
            <Textarea
              value={form.napomena}
              onChange={(e) => setForm({ ...form, napomena: e.target.value })}
              rows={3}
              maxLength={500}
            />
          </Field>

          <Field label="URL izvještaja (PDF)" error={errors["input.pdfUrl"]?.[0]}>
            <Input
              value={form.pdfUrl}
              onChange={(e) => setForm({ ...form, pdfUrl: e.target.value })}
              placeholder="https://…"
              maxLength={500}
            />
          </Field>

          <label className="flex items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              checked={form.objavljena}
              onChange={(e) => setForm({ ...form, objavljena: e.target.checked })}
              className="h-4 w-4 accent-brand-600"
            />
            Objavljena
          </label>

          <Button type="submit" className="w-full" disabled={mutation.isPending}>
            {mutation.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        </form>
      </div>
    </div>
  );
}
