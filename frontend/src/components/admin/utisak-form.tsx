"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { queryKeys } from "@/lib/api/hooks";
import type { UtisakInput } from "@/lib/api/contracts";
import type { Utisak } from "@/types/domain";

const MAKS_TEKST = 500;
const PRAZNO = { ime: "", mjesto: "", tekst: "", objavljen: true, redoslijed: "0" };

export function UtisakForm({
  open,
  onClose,
  utisak,
}: {
  open: boolean;
  onClose: () => void;
  utisak?: Utisak | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState(PRAZNO);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      utisak
        ? {
            ime: utisak.ime,
            mjesto: utisak.mjesto,
            tekst: utisak.tekst,
            objavljen: utisak.objavljen,
            redoslijed: String(utisak.redoslijed),
          }
        : PRAZNO,
    );
  }, [open, utisak]);

  const mutation = useMutation({
    mutationFn: (body: UtisakInput) =>
      utisak ? adminApi.updateUtisak(utisak.id, body) : adminApi.createUtisak(body),
    onSuccess: () => {
      // Oba ključa: admin lista i javna lista na početnoj (Review Focus 5).
      qc.invalidateQueries({ queryKey: queryKeys.adminUtisci() });
      qc.invalidateQueries({ queryKey: queryKeys.utisci() });
      show(utisak ? "Utisak ažuriran" : "Utisak dodan");
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
      ime: form.ime,
      mjesto: form.mjesto || undefined,
      tekst: form.tekst,
      objavljen: form.objavljen,
      redoslijed: Number(form.redoslijed) || 0,
    });
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50">
      <button aria-label="Zatvori" onClick={onClose} className="absolute inset-0 bg-black/40" />
      <div className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-cream shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-6 py-4">
          <h2 className="font-display text-lg font-bold">{utisak ? "Uredi utisak" : "Novi utisak"}</h2>
          <button onClick={onClose} className="rounded-full p-1.5 hover:bg-sand" aria-label="Zatvori">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
          <Field label="Ime kupca" required error={errors["input.ime"]?.[0]}>
            <Input
              value={form.ime}
              onChange={(e) => setForm({ ...form, ime: e.target.value })}
              placeholder="npr. Marija P."
              maxLength={60}
            />
          </Field>

          <Field label="Mjesto" error={errors["input.mjesto"]?.[0]}>
            <Input
              value={form.mjesto}
              onChange={(e) => setForm({ ...form, mjesto: e.target.value })}
              placeholder="npr. Borik"
              maxLength={60}
            />
          </Field>

          <Field label="Utisak" required error={errors["input.tekst"]?.[0]}>
            <Textarea
              value={form.tekst}
              onChange={(e) => setForm({ ...form, tekst: e.target.value })}
              rows={5}
              maxLength={MAKS_TEKST}
            />
            <p className="mt-1 text-right text-xs text-muted">
              {form.tekst.length}/{MAKS_TEKST}
            </p>
          </Field>

          <Field label="Redoslijed (manji ide prvi)" error={errors["input.redoslijed"]?.[0]}>
            <Input
              type="number"
              min={0}
              max={999}
              value={form.redoslijed}
              onChange={(e) => setForm({ ...form, redoslijed: e.target.value })}
            />
          </Field>

          <label className="flex items-center gap-2 text-sm text-ink">
            <input
              type="checkbox"
              checked={form.objavljen}
              onChange={(e) => setForm({ ...form, objavljen: e.target.checked })}
              className="h-4 w-4 accent-brand-600"
            />
            Objavljen na početnoj
          </label>

          <Button type="submit" className="w-full" disabled={mutation.isPending}>
            {mutation.isPending ? "Čuvam…" : "Sačuvaj"}
          </Button>
        </form>
      </div>
    </div>
  );
}
