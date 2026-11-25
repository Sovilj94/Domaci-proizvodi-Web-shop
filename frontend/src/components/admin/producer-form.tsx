"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { SlikaPolje } from "@/components/admin/slika-polje";
import { Button } from "@/components/ui/button";
import { Field, Input, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { ProducerInput } from "@/lib/api/contracts";
import type { AdminProducer } from "@/types/domain";

const EMPTY = {
  name: "",
  village: "",
  description: "",
  phone: "",
  photoUrl: "",
  nosilacIme: "",
  bpg: "",
  jmbg: "",
  adresa: "",
  ziroRacun: "",
  banka: "",
};

export function ProducerForm({
  open,
  onClose,
  producer,
}: {
  open: boolean;
  onClose: () => void;
  producer?: AdminProducer | null;
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
            nosilacIme: producer.nosilacIme ?? "",
            bpg: producer.bpg ?? "",
            jmbg: producer.jmbg ?? "",
            adresa: producer.adresa ?? "",
            ziroRacun: producer.ziroRacun ?? "",
            banka: producer.banka ?? "",
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
      nosilacIme: form.nosilacIme || undefined,
      bpg: form.bpg || undefined,
      jmbg: form.jmbg || undefined,
      adresa: form.adresa || undefined,
      ziroRacun: form.ziroRacun || undefined,
      banka: form.banka || undefined,
    });
  }

  if (!open) return null;

  return (
    <AdminLadica naslov={producer ? "Uredi proizvođača" : "Novi proizvođač"} onClose={onClose}>
      <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-4 sm:p-6">
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

        <SlikaPolje
          label="Fotografija (opcionalno)"
          folder="producers"
          value={form.photoUrl}
          onChange={(photoUrl) => setForm((f) => ({ ...f, photoUrl }))}
        />

        <fieldset className="space-y-4 rounded-card border border-line p-4">
          <legend className="px-1 text-sm font-semibold text-ink">Podaci za otkupni list</legend>
          <p className="text-xs text-muted">
            Opciono. Štampaju se na otkupnom listu koji gazdinstvo potpisuje pri predaji robe; prazno
            polje ostaje linija za upis rukom. JMBG i račun vidi samo admin.
          </p>
          <Field label="Nosilac gazdinstva" error={errors["input.nosilacIme"]?.[0]}>
            <Input
              value={form.nosilacIme}
              onChange={(e) => setForm({ ...form, nosilacIme: e.target.value })}
              placeholder="Ime i prezime"
            />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Broj gazdinstva (BPG)" error={errors["input.bpg"]?.[0]}>
              <Input
                value={form.bpg}
                inputMode="numeric"
                onChange={(e) => setForm({ ...form, bpg: e.target.value })}
              />
            </Field>
            <Field label="JMBG nosioca" error={errors["input.jmbg"]?.[0]}>
              <Input
                value={form.jmbg}
                inputMode="numeric"
                maxLength={13}
                onChange={(e) => setForm({ ...form, jmbg: e.target.value })}
              />
            </Field>
          </div>
          <Field label="Adresa" error={errors["input.adresa"]?.[0]}>
            <Input
              value={form.adresa}
              onChange={(e) => setForm({ ...form, adresa: e.target.value })}
              placeholder="Ulica i broj, mjesto"
            />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Žiro račun" error={errors["input.ziroRacun"]?.[0]}>
              <Input
                value={form.ziroRacun}
                onChange={(e) => setForm({ ...form, ziroRacun: e.target.value })}
              />
            </Field>
            <Field label="Banka" error={errors["input.banka"]?.[0]}>
              <Input value={form.banka} onChange={(e) => setForm({ ...form, banka: e.target.value })} />
            </Field>
          </div>
        </fieldset>
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
