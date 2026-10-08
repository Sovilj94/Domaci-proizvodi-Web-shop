"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { useDeliveryZones, queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { AddressInput } from "@/lib/api/contracts";
import type { Address } from "@/types/domain";

const EMPTY = {
  label: "",
  fullAddress: "",
  deliveryZoneId: "",
};

/**
 * Panel koji klizi s desne strane — isti obrazac kao `components/admin/product-form.tsx`.
 * Koordinate se ovdje ne unose ručno (birač na mapi je zaseban posao, nije u ovom tasku);
 * kad se postojeća adresa uređuje i već ima tačku, ta tačka putuje kroz izmjenu
 * nepromijenjena umjesto da se tiho izgubi.
 */
export function AdresaForma({
  open,
  onClose,
  adresa,
}: {
  open: boolean;
  onClose: () => void;
  adresa?: Address | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const zones = useDeliveryZones();
  const [form, setForm] = useState(EMPTY);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setErrors({});
    setError(null);
    setForm(
      adresa
        ? {
            label: adresa.label ?? "",
            fullAddress: adresa.fullAddress,
            deliveryZoneId: adresa.deliveryZoneId ?? "",
          }
        : EMPTY,
    );
  }, [open, adresa]);

  const mutation = useMutation({
    mutationFn: (body: AddressInput) =>
      adresa ? accountApi.izmijeniAdresu(adresa.id, body) : accountApi.dodajAdresu(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.adrese() });
      show(adresa ? "Adresa ažurirana" : "Adresa dodana");
      onClose();
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) {
        setErrors(err.fieldErrors);
        if (Object.keys(err.fieldErrors).length === 0) setError(err.message);
      } else {
        setError("Došlo je do greške.");
      }
    },
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    setError(null);
    mutation.mutate({
      label: form.label || undefined,
      fullAddress: form.fullAddress,
      deliveryZoneId: form.deliveryZoneId || undefined,
      // Prosljeđuje se samo ako adresa koja se uređuje već ima tačku — birač na mapi
      // nije u ovom tasku, pa se tačka ovdje ne unosi ručno (vidi napomenu iznad).
      latitude: adresa?.latitude,
      longitude: adresa?.longitude,
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
            {adresa ? "Uredi adresu" : "Nova adresa"}
          </h2>
          <button onClick={onClose} className="rounded-full p-1.5 hover:bg-sand">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
          <Field label="Naziv (opcionalno)" error={errors.label?.[0]}>
            <Input
              value={form.label}
              onChange={(e) => setForm({ ...form, label: e.target.value })}
              placeholder="Kuća"
            />
          </Field>

          <Field label="Adresa" required error={errors.fullAddress?.[0]}>
            <Input
              value={form.fullAddress}
              onChange={(e) => setForm({ ...form, fullAddress: e.target.value })}
            />
          </Field>

          <Field label="Zona dostave" error={errors.deliveryZoneId?.[0]}>
            <Select
              value={form.deliveryZoneId}
              onChange={(e) => setForm({ ...form, deliveryZoneId: e.target.value })}
            >
              <option value="">Odaberite…</option>
              {zones.data?.map((z) => (
                <option key={z.id} value={z.id}>
                  {z.name}
                </option>
              ))}
            </Select>
          </Field>

          {error && (
            <p className="rounded-lg bg-red-50 px-3 py-2 text-sm font-medium text-red-700">
              {error}
            </p>
          )}
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
