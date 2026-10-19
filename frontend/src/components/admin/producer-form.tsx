"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Button } from "@/components/ui/button";
import { Field, Input, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { ProducerInput } from "@/lib/api/contracts";
import type { Producer } from "@/types/domain";

const EMPTY = {
  name: "",
  village: "",
  description: "",
  phone: "",
  photoUrl: "",
};

export function ProducerForm({
  open,
  onClose,
  producer,
}: {
  open: boolean;
  onClose: () => void;
  producer?: Producer | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      producer
        ? {
            name: producer.name,
            village: producer.village,
            description: producer.description ?? "",
            phone: producer.phone ?? "",
            photoUrl: producer.photoUrl ?? "",
          }
        : EMPTY,
    );
  }, [open, producer]);

  const mutation = useMutation({
    mutationFn: (body: ProducerInput) =>
      producer ? adminApi.updateProducer(producer.id, body) : adminApi.createProducer(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["producers"] });
      qc.invalidateQueries({ queryKey: ["products"] });
      show(producer ? "Proizvođač ažuriran" : "Proizvođač dodan");
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
      village: form.village,
      description: form.description || undefined,
      phone: form.phone || undefined,
      photoUrl: form.photoUrl || undefined,
    });
  }

  if (!open) return null;

  return (
    <AdminLadica naslov={producer ? "Uredi proizvođača" : "Novi proizvođač"} onClose={onClose}>
      <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
        <Field label="Naziv" required error={errors["input.name"]?.[0]}>
          <Input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            placeholder="npr. Porodica Kovačević"
          />
        </Field>

        <Field label="Selo / mjesto" required error={errors["input.village"]?.[0]}>
          <Input
            value={form.village}
            onChange={(e) => setForm({ ...form, village: e.target.value })}
            placeholder="npr. Han Kola"
          />
        </Field>

        <Field label="Telefon">
          <Input
            value={form.phone}
            onChange={(e) => setForm({ ...form, phone: e.target.value })}
            placeholder="065 000 000"
          />
        </Field>

        <Field label="Opis">
          <Textarea
            value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            placeholder="Nekoliko riječi o domaćinstvu…"
          />
        </Field>

        <Field label="URL fotografije (opcionalno)">
          <Input
            value={form.photoUrl}
            onChange={(e) => setForm({ ...form, photoUrl: e.target.value })}
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
