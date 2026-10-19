"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { CategoryInput } from "@/lib/api/contracts";
import type { Category } from "@/types/domain";

const EMPTY = { name: "", imageUrl: "", sortOrder: "" };

export function CategoryForm({
  open,
  onClose,
  category,
}: {
  open: boolean;
  onClose: () => void;
  category?: Category | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      category
        ? {
            name: category.name,
            imageUrl: category.imageUrl ?? "",
            sortOrder: String(category.sortOrder),
          }
        : EMPTY,
    );
  }, [open, category]);

  const mutation = useMutation({
    mutationFn: (body: CategoryInput) =>
      category ? adminApi.updateCategory(category.id, body) : adminApi.createCategory(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["categories"] });
      show(category ? "Kategorija ažurirana" : "Kategorija dodana");
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
      imageUrl: form.imageUrl || undefined,
      sortOrder: Number(form.sortOrder || 0),
    });
  }

  if (!open) return null;

  return (
    <AdminLadica naslov={category ? "Uredi kategoriju" : "Nova kategorija"} onClose={onClose}>
      <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
        <Field label="Naziv" required error={errors["input.name"]?.[0]}>
          <Input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
          />
        </Field>

        <Field label="Redoslijed" error={errors["input.sortOrder"]?.[0]}>
          <Input
            type="number"
            min="0"
            value={form.sortOrder}
            onChange={(e) => setForm({ ...form, sortOrder: e.target.value })}
            placeholder="0"
          />
        </Field>

        <Field label="URL slike (opcionalno)">
          <Input
            value={form.imageUrl}
            onChange={(e) => setForm({ ...form, imageUrl: e.target.value })}
            placeholder="https://…"
          />
        </Field>
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
