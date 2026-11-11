"use client";

import { useCallback, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { AddressPicker } from "@/components/shop/address-picker";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { NaseljeOznaka } from "@/components/shop/naselje-oznaka";
import { useToast } from "@/components/ui/toast";
import { useNaseljeZaTacku, queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { AddressInput } from "@/lib/api/contracts";
import type { Address } from "@/types/domain";

function pocetnoStanje(adresa?: Address | null) {
  return {
    label: adresa?.label ?? "",
    fullAddress: adresa?.fullAddress ?? "",
    latitude: adresa?.latitude,
    longitude: adresa?.longitude,
  };
}

/**
 * Panel koji klizi s desne strane — isti obrazac kao `components/admin/product-form.tsx`.
 *
 * Adresa se unosi kroz `AddressPicker`, isti birač koji koriste checkout i admin: tekst →
 * geokodiranje kroz naš `/geocode` → pin koji se može prevući. Tačka je ono što adresar
 * vrijedi vozaču i iz nje sistem određuje naselje, pa je obavezna: kad pretraga ne nađe adresu,
 * kupac spusti pin na mapu.
 *
 * Sadržaj je izdvojen u `Forma` i montira se iznova za svaku adresu (`key`), umjesto da
 * se stanje puni efektom nakon otvaranja. Razlog je birač: on čita `value` i
 * `initialCoordinates` na mountu, pa bi ga punjenje kroz efekat zateklo sa praznim
 * poljem, a sledeća izmjena bi mu izgledala kao da je kupac počeo da kuca — poništio bi
 * sačuvanu tačku i krenuo da geokodira iznova. Ovako panel ne postoji dok se ne otvori,
 * i svako otvaranje počinje od čistog stanja, bez efekta koji to dopravlja.
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
  if (!open) return null;
  return <Forma key={adresa?.id ?? "nova"} onClose={onClose} adresa={adresa} />;
}

function Forma({
  onClose,
  adresa,
}: {
  onClose: () => void;
  adresa?: Address | null;
}) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [form, setForm] = useState(() => pocetnoStanje(adresa));
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);

  // Birač javlja tačku (ili `null` kad tekst adrese krene da se mijenja). Pola tačke
  // backend odbija — `AddressRules` traži i širinu i dužinu ili nijednu — pa se obje
  // postavljaju i gase zajedno, u jednom setState.
  // Adresa van grada dostave: birač prikazuje upozorenje, a čuvanje čeka drugu adresu.
  const [vanPodrucja, setVanPodrucja] = useState(false);
  const postaviTacku = useCallback((coords: { lat: number; lon: number } | null) => {
    setForm((f) => ({ ...f, latitude: coords?.lat, longitude: coords?.lon }));
  }, []);

  // Naselje određuje sistem iz tačke; server isto provjerava pri čuvanju.
  const naselje = useNaseljeZaTacku(form.latitude, form.longitude);
  const imaTacku = form.latitude !== undefined && form.longitude !== undefined;

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
      latitude: form.latitude,
      longitude: form.longitude,
    });
  }

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
            <AddressPicker
              id="adresa"
              value={form.fullAddress}
              onChange={(value) => setForm((f) => ({ ...f, fullAddress: value }))}
              onCoordinates={postaviTacku}
              onVanPodrucja={setVanPodrucja}
              initialCoordinates={
                adresa?.latitude != null && adresa?.longitude != null
                  ? { lat: adresa.latitude, lon: adresa.longitude }
                  : undefined
              }
            />
          </Field>

          <NaseljeOznaka naselje={naselje} imaTacku={imaTacku} />

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
          <Button className="flex-1" onClick={submit} disabled={mutation.isPending || vanPodrucja || !naselje.data}>
            {mutation.isPending ? "Spremam…" : "Spremi"}
          </Button>
        </div>
      </div>
    </div>
  );
}
