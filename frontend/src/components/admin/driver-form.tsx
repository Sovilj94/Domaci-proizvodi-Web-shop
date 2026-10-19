"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { Driver, DriverInput } from "@/lib/api/contracts";

const EMPTY = {
  name: "",
  phone: "",
  vozilo: "",
  registracija: "",
  aktivan: true,
};

export function DriverForm({
  open,
  onClose,
  driver,
}: {
  open: boolean;
  onClose: () => void;
  driver?: Driver | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setForm(
      driver
        ? {
            name: driver.name,
            phone: driver.phone,
            vozilo: driver.vozilo ?? "",
            registracija: driver.registracija ?? "",
            aktivan: driver.aktivan,
          }
        : EMPTY,
    );
  }, [open, driver]);

  const mutation = useMutation({
    mutationFn: (body: DriverInput) =>
      driver ? adminApi.updateDriver(driver.id, body) : adminApi.createDriver(body),
    onSuccess: () => {
      // Dispečerski ekran crta tabove po vozačima, pa i njega treba osvježiti.
      qc.invalidateQueries({ queryKey: ["admin", "drivers"] });
      qc.invalidateQueries({ queryKey: ["admin", "routes"] });
      show(driver ? "Vozač ažuriran" : "Vozač dodan");
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
      phone: form.phone,
      vozilo: form.vozilo || undefined,
      registracija: form.registracija || undefined,
      aktivan: form.aktivan,
    });
  }

  if (!open) return null;

  return (
    <AdminLadica naslov={driver ? "Uredi vozača" : "Novi vozač"} onClose={onClose}>
      <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
        <Field label="Ime i prezime" required error={errors["input.name"]?.[0]}>
          <Input
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            placeholder="npr. Mladen Vozač"
          />
        </Field>

        <Field label="Telefon" required error={errors["input.phone"]?.[0]}>
          <Input
            value={form.phone}
            onChange={(e) => setForm({ ...form, phone: e.target.value })}
            placeholder="065 000 000"
          />
        </Field>

        <Field label="Vozilo">
          <Input
            value={form.vozilo}
            onChange={(e) => setForm({ ...form, vozilo: e.target.value })}
            placeholder="npr. Kombi 1"
          />
        </Field>

        <Field label="Registracija">
          <Input
            value={form.registracija}
            onChange={(e) => setForm({ ...form, registracija: e.target.value })}
            placeholder="npr. A12-B-345"
          />
        </Field>

        <label className="flex items-center gap-2 text-sm font-medium">
          <input
            type="checkbox"
            checked={form.aktivan}
            onChange={(e) => setForm({ ...form, aktivan: e.target.checked })}
            className="h-4 w-4 accent-brand-600"
          />
          Aktivan
        </label>
        <p className="text-xs text-muted">
          Neaktivan vozač nema tab na dispečerskom ekranu i ne prima nove narudžbe. Rute koje
          već ima ostaju netaknute.
        </p>

        <p className="rounded-card bg-sand px-3 py-2 text-xs text-muted">
          Vozač unesen ovdje nema nalog za mobilnu aplikaciju — postoji za dispečerski ekran i
          rute. Nalog za prijavu se dodjeljuje odvojeno.
        </p>
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
