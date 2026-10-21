"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Button } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { useCategories, useProducers } from "@/lib/api/hooks";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { ProductInput } from "@/lib/api/contracts";
import type { Product, Unit } from "@/types/domain";
import { cn } from "@/lib/utils";
import { formatMarza, marza, tonMarze } from "@/lib/marza";

const TON_MARZE = {
  nema: "bg-sand text-muted",
  losa: "bg-red-50 text-red-700",
  tijesna: "bg-honey-100 text-honey-700",
  dobra: "bg-brand-100 text-brand-700",
} as const;

function MarzaOznaka({ prodajna, nabavna }: { prodajna: number; nabavna: number | null }) {
  const m = marza(prodajna, nabavna);
  return (
    <p className={cn("mb-1 rounded-xl px-3 py-2.5 text-sm", TON_MARZE[tonMarze(m)])}>
      Marža: <strong>{formatMarza(m)}</strong>
      {m != null && <span className="block text-xs">zarada {(prodajna - (nabavna ?? 0)).toFixed(2)} KM po jedinici</span>}
    </p>
  );
}

const EMPTY = {
  name: "",
  price: "",
  nabavnaCijena: "",
  unit: "kg" as Unit,
  stock: "",
  categoryId: "",
  producerId: "",
  description: "",
  imageUrl: "",
  isAvailable: true,
  isFeatured: false,
  mozeUKorpicu: false,
  prioritetUKorpici: "0",
  mjeseci: [] as number[],
  boja: "",
};

/** Kratki nazivi mjeseci za 12 checkbox-a — indeks 0 je januar. */
const MJESECI_NAZIVI = [
  "Jan", "Feb", "Mar", "Apr", "Maj", "Jun",
  "Jul", "Avg", "Sep", "Okt", "Nov", "Dec",
];

export function ProductForm({
  open,
  onClose,
  product,
}: {
  open: boolean;
  onClose: () => void;
  product?: Product | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const categories = useCategories();
  const producers = useProducers();
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      product
        ? {
            name: product.name,
            price: String(product.price),
            nabavnaCijena: product.nabavnaCijena != null ? String(product.nabavnaCijena) : "",
            unit: product.unit,
            stock: String(product.stock),
            categoryId: product.categoryId,
            producerId: product.producerId,
            description: product.description,
            imageUrl: product.imageUrl ?? "",
            isAvailable: product.isAvailable,
            isFeatured: product.isFeatured ?? false,
            mozeUKorpicu: product.mozeUKorpicu ?? false,
            prioritetUKorpici: String(product.prioritetUKorpici ?? 0),
            mjeseci: product.mjeseci ?? [],
            boja: product.zadanaBoja ?? "",
          }
        : EMPTY,
    );
  }, [open, product]);

  const mutation = useMutation({
    mutationFn: (body: ProductInput) =>
      product ? adminApi.updateProduct(product.id, body) : adminApi.createProduct(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "products"] });
      qc.invalidateQueries({ queryKey: ["products"] });
      qc.invalidateQueries({ queryKey: ["categories"] });
      show(product ? "Proizvod ažuriran" : "Proizvod dodan");
      onClose();
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) setErrors(err.fieldErrors);
    },
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    mutation.mutate({
      name: form.name,
      price: Number(form.price),
      // Prazno polje: kod novog proizvoda nema nabavne, kod postojećeg se unesena briše.
      nabavnaCijena: form.nabavnaCijena.trim() ? Number(form.nabavnaCijena) : undefined,
      ukloniNabavnuCijenu:
        product?.nabavnaCijena != null && !form.nabavnaCijena.trim() ? true : undefined,
      unit: form.unit,
      stock: Number(form.stock || 0),
      categoryId: form.categoryId,
      producerId: form.producerId,
      description: form.description,
      imageUrl: form.imageUrl || undefined,
      isAvailable: form.isAvailable,
      isFeatured: form.isFeatured,
      mozeUKorpicu: form.mozeUKorpicu,
      prioritetUKorpici: Number(form.prioritetUKorpici || 0),
      mjeseci: form.mjeseci,
      boja: form.boja.trim(),
    });
  }

  function prekidajMjesec(mjesec: number) {
    setForm((f) => ({
      ...f,
      mjeseci: f.mjeseci.includes(mjesec)
        ? f.mjeseci.filter((m) => m !== mjesec)
        : [...f.mjeseci, mjesec].sort((a, b) => a - b),
    }));
  }

  if (!open) return null;

  return (
    <AdminLadica naslov={product ? "Uredi proizvod" : "Novi proizvod"} onClose={onClose}>
      <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
        <Field label="Naziv" required error={errors.name?.[0]}>
          <Input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
          />
        </Field>

        <div className="grid grid-cols-3 gap-3">
          <Field label="Cijena (KM)" required error={errors.price?.[0]}>
            <Input
              type="number"
              step="0.01"
              min="0"
              value={form.price}
              onChange={(e) => setForm({ ...form, price: e.target.value })}
            />
          </Field>
          <Field label="Jedinica" required error={errors.unit?.[0]}>
            <Select
              value={form.unit}
              onChange={(e) => setForm({ ...form, unit: e.target.value as Unit })}
            >
              <option value="kg">kg</option>
              <option value="kom">kom</option>
              <option value="l">l</option>
            </Select>
          </Field>
          <Field label="Zaliha">
            <Input
              type="number"
              min="0"
              value={form.stock}
              onChange={(e) => setForm({ ...form, stock: e.target.value })}
            />
          </Field>
        </div>

        <div className="grid grid-cols-2 items-end gap-3">
          <Field
            label="Nabavna cijena (KM)"
            error={errors.nabavnaCijena?.[0] ?? errors["input.nabavnaCijena"]?.[0]}
          >
            <Input
              type="number"
              step="0.01"
              min="0"
              value={form.nabavnaCijena}
              onChange={(e) => setForm({ ...form, nabavnaCijena: e.target.value })}
              placeholder="nije unesena"
            />
          </Field>
          <MarzaOznaka prodajna={Number(form.price)} nabavna={form.nabavnaCijena.trim() ? Number(form.nabavnaCijena) : null} />
        </div>

        <Field label="Kategorija" required error={errors.categoryId?.[0]}>
          <Select
            value={form.categoryId}
            onChange={(e) => setForm({ ...form, categoryId: e.target.value })}
          >
            <option value="">Odaberite…</option>
            {categories.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Proizvođač" required error={errors.producerId?.[0]}>
          <Select
            value={form.producerId}
            onChange={(e) => setForm({ ...form, producerId: e.target.value })}
          >
            <option value="">Odaberite…</option>
            {producers.data?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} · {p.village}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Opis">
          <Textarea
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
          />
        </Field>

        <Field label="URL slike (opcionalno)">
          <Input
            value={form.imageUrl}
            onChange={(e) => setForm({ ...form, imageUrl: e.target.value })}
            placeholder="https://…"
          />
        </Field>

        <Field label="Boja pločice (opcionalno)" htmlFor="proizvod-boja" error={errors.boja?.[0]}>
          <div className="flex items-center gap-3">
            <input
              type="color"
              aria-label="Izaberi boju"
              value={/^#[0-9a-f]{6}$/i.test(form.boja) ? form.boja : (product?.boja ?? "#f4c27a")}
              onChange={(e) => setForm({ ...form, boja: e.target.value.toUpperCase() })}
              className="h-10 w-14 cursor-pointer rounded-lg border border-line bg-white"
            />
            <Input
              id="proizvod-boja"
              value={form.boja}
              onChange={(e) => setForm({ ...form, boja: e.target.value })}
              placeholder={product?.boja ? `Iz palete: ${product.boja}` : "#RRGGBB — prazno = iz palete"}
            />
          </div>
        </Field>

        <div className="flex gap-6">
          <label className="flex items-center gap-2 text-sm font-medium">
            <input
              type="checkbox"
              checked={form.isAvailable}
              onChange={(e) => setForm({ ...form, isAvailable: e.target.checked })}
              className="h-4 w-4 accent-brand-600"
            />
            Dostupno
          </label>
          <label className="flex items-center gap-2 text-sm font-medium">
            <input
              type="checkbox"
              checked={form.isFeatured}
              onChange={(e) => setForm({ ...form, isFeatured: e.target.checked })}
              className="h-4 w-4 accent-brand-600"
            />
            Izdvojeno
          </label>
        </div>

        {/* Korpice (P1): bez ovih polja artikal dodat ovdje nikad ne može ući u bazen
            generatora korpica (vidi GenerisiSedmicniSadrzajHandler) — kolone su postojale
            na Product-u, ali admin API ih nije primao. */}
        <div className="rounded-card border border-line bg-sand/50 p-4">
          <p className="mb-3 text-sm font-semibold text-ink">Korpice</p>

          <label className="flex items-center gap-2 text-sm font-medium">
            <input
              type="checkbox"
              checked={form.mozeUKorpicu}
              onChange={(e) => setForm({ ...form, mozeUKorpicu: e.target.checked })}
              className="h-4 w-4 accent-brand-600"
            />
            Smije ući u sadržaj korpice
          </label>

          <div className="mt-3">
            <Field label="Prioritet u korpici">
              <Input
                type="number"
                min="0"
                step="1"
                value={form.prioritetUKorpici}
                onChange={(e) => setForm({ ...form, prioritetUKorpici: e.target.value })}
              />
            </Field>
            <p className="mt-1 text-xs text-muted">
              0 = osnovni artikal (ide skoro uvijek), veći broj = dopuna.
            </p>
          </div>

          <div className="mt-3">
            <p className="mb-1.5 text-sm font-medium">
              Sezona (mjeseci) — praznina znači cijele godine
            </p>
            <div className="grid grid-cols-6 gap-1.5 sm:grid-cols-12">
              {MJESECI_NAZIVI.map((naziv, i) => {
                const mjesec = i + 1;
                const aktivan = form.mjeseci.includes(mjesec);
                return (
                  <button
                    key={mjesec}
                    type="button"
                    onClick={() => prekidajMjesec(mjesec)}
                    className={cn(
                      "rounded-lg border px-1.5 py-1 text-xs font-medium transition",
                      aktivan
                        ? "border-brand-600 bg-brand-600 text-white"
                        : "border-line bg-white text-muted hover:border-brand-400",
                    )}
                  >
                    {naziv}
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      </form>

      <div className="flex gap-3 border-t border-line p-4">
        <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
          Otkaži
        </Button>
        <Button className="flex-1" onClick={submit} disabled={mutation.isPending}>
          {mutation.isPending ? "Spremam…" : "Spremi"}
        </Button>
      </div>
    </AdminLadica>
  );
}
