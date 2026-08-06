"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { useCategories, useProducers } from "@/lib/api/hooks";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { ProductInput } from "@/lib/api/contracts";
import type { Product, Unit } from "@/types/domain";

const EMPTY = {
  name: "",
  price: "",
  unit: "kg" as Unit,
  stock: "",
  categoryId: "",
  producerId: "",
  description: "",
  imageUrl: "",
  isAvailable: true,
  isFeatured: false,
};

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
            unit: product.unit,
            stock: String(product.stock),
            categoryId: product.categoryId,
            producerId: product.producerId,
            description: product.description,
            imageUrl: product.imageUrl ?? "",
            isAvailable: product.isAvailable,
            isFeatured: product.isFeatured ?? false,
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
      unit: form.unit,
      stock: Number(form.stock || 0),
      categoryId: form.categoryId,
      producerId: form.producerId,
      description: form.description,
      imageUrl: form.imageUrl || undefined,
      isAvailable: form.isAvailable,
      isFeatured: form.isFeatured,
    });
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50">
      <button
        aria-label="Zatvori"
        onClick={onClose}
        className="absolute inset-0 bg-black/40"
      />
      <div className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-cream shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-6 py-4">
          <h2 className="font-display text-lg font-bold">
            {product ? "Uredi proizvod" : "Novi proizvod"}
          </h2>
          <button onClick={onClose} className="rounded-full p-1.5 hover:bg-sand">
            <X className="h-5 w-5" />
          </button>
        </div>

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
        </form>

        <div className="flex gap-3 border-t border-line p-4">
          <Button variant="secondary" className="flex-1" onClick={onClose} type="button">
            Otkaži
          </Button>
          <Button className="flex-1" onClick={submit} disabled={mutation.isPending}>
            {mutation.isPending ? "Spremam…" : "Spremi"}
          </Button>
        </div>
      </div>
    </div>
  );
}
