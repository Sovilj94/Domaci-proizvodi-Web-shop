"use client";

import { useCallback, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { AddressPicker } from "@/components/shop/address-picker";
import { Button } from "@/components/ui/button";
import { Field, Select, Textarea } from "@/components/ui/field";
import { useToast } from "@/components/ui/toast";
import { useDeliverySlots, useDeliveryZones, useKorpiceList, queryKeys } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { pogodiZonu } from "@/lib/zone";
import { formatPrice } from "@/lib/utils";
import type { PretplataInput } from "@/lib/api/contracts";
import type { PlanPretplate, Pretplata } from "@/types/domain";
import { cijenaPlana, isporuka, obavezaPlana, PLAN_TEKST } from "@/lib/planovi";

function pocetnoStanje(pretplata?: Pretplata | null, korpicaId?: string) {
  return {
    productId: pretplata?.productId ?? korpicaId ?? "",
    deliveryZoneId: pretplata?.deliveryZoneId ?? "",
    deliverySlotId: pretplata?.deliverySlotId ?? "",
    deliveryAddress: pretplata?.deliveryAddress ?? "",
    latitude: pretplata?.latitude,
    longitude: pretplata?.longitude,
    napomena: pretplata?.napomena ?? "",
  };
}

/**
 * Panel za novu pretplatu i za izmjenu postojeće — isti obrazac kao `adresa-forma.tsx`,
 * uključujući i razlog zašto je sadržaj izdvojen u komponentu sa `key`-em: `AddressPicker`
 * čita `value` i `initialCoordinates` na mountu, pa punjenje stanja efektom znači da ga
 * zatekne prazno polje i da prvu izmjenu pročita kao kucanje, poništavajući sačuvanu tačku.
 */
export function PretplataForma({
  open,
  onClose,
  pretplata,
  korpicaId,
  plan,
}: {
  open: boolean;
  onClose: () => void;
  pretplata?: Pretplata | null;
  /** Korpica izabrana izvan forme (dugme „Pretplati se" na stranici korpice). */
  korpicaId?: string;
  /** Plan izabran na stranici korpice (R2); samo za novu pretplatu. */
  plan?: PlanPretplate;
}) {
  if (!open) return null;
  return (
    <Forma
      key={pretplata?.id ?? korpicaId ?? "nova"}
      onClose={onClose}
      pretplata={pretplata}
      korpicaId={korpicaId}
      plan={plan}
    />
  );
}

function Forma({
  onClose,
  pretplata,
  korpicaId,
  plan,
}: {
  onClose: () => void;
  pretplata?: Pretplata | null;
  korpicaId?: string;
  plan?: PlanPretplate;
}) {
  // Plan se bira na stranici korpice; postojeća pretplata zadržava svoj (izmjena plana nije u opsegu).
  const izabraniPlan: PlanPretplate = pretplata?.plan ?? plan ?? "Nedjeljna";
  const qc = useQueryClient();
  const { show } = useToast();
  const zones = useDeliveryZones();
  const slots = useDeliverySlots();
  const korpice = useKorpiceList();
  const [form, setForm] = useState(() => pocetnoStanje(pretplata, korpicaId));
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [error, setError] = useState<string | null>(null);

  const postaviTacku = useCallback((coords: { lat: number; lon: number } | null) => {
    setForm((f) => ({ ...f, latitude: coords?.lat, longitude: coords?.lon }));
  }, []);

  const predloziZonu = useCallback(
    (localities: string[]) => {
      const match = pogodiZonu(localities, zones.data);
      if (match) setForm((f) => (f.deliveryZoneId ? f : { ...f, deliveryZoneId: match.id }));
    },
    [zones.data],
  );

  const mutation = useMutation({
    mutationFn: (body: PretplataInput) =>
      pretplata
        ? accountApi.izmijeniPretplatu(pretplata.id, body)
        : accountApi.dodajPretplatu(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: queryKeys.pretplate() });
      show(pretplata ? "Pretplata izmijenjena" : "Pretplata je aktivna");
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
      productId: form.productId,
      deliveryZoneId: form.deliveryZoneId,
      deliverySlotId: form.deliverySlotId,
      deliveryAddress: form.deliveryAddress,
      latitude: form.latitude,
      longitude: form.longitude,
      napomena: form.napomena || undefined,
      plan: pretplata ? undefined : izabraniPlan,
    });
  }

  const izabrana = korpice.data?.find((k) => k.id === form.productId);

  return (
    <div className="fixed inset-0 z-50">
      <button aria-label="Zatvori" onClick={onClose} className="absolute inset-0 bg-black/40" />
      <div className="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-cream shadow-xl">
        <div className="flex items-center justify-between border-b border-line px-6 py-4">
          <h2 className="font-display text-lg font-bold">
            {pretplata ? "Uredi pretplatu" : "Nova pretplata"}
          </h2>
          <button onClick={onClose} className="rounded-full p-1.5 hover:bg-sand">
            <X className="h-5 w-5" />
          </button>
        </div>

        <form onSubmit={submit} className="flex-1 space-y-4 overflow-y-auto p-6">
          <Field label="Korpica" required error={errors.productId?.[0]}>
            <Select
              value={form.productId}
              onChange={(e) => setForm({ ...form, productId: e.target.value })}
            >
              <option value="">Odaberite…</option>
              {korpice.data
                ?.filter((k) => k.dostupna || k.id === form.productId)
                .map((k) => (
                  <option key={k.id} value={k.id}>
                    {k.naziv} — {formatPrice(cijenaPlana(k, izabraniPlan))}
                  </option>
                ))}
            </Select>
          </Field>

          {/* Cijene dolaze sa servera (`PretplatnaCijena`), ne računaju se ovdje — kopija
              tog pravila u pregledaču bi se razišla sa onim što se naplati. */}
          {izabrana && (
            <p className="rounded-lg bg-beige px-3 py-2 text-sm text-ink">
              Plan <strong>{PLAN_TEKST[izabraniPlan]}</strong>:{" "}
              <span className="font-semibold">{formatPrice(cijenaPlana(izabrana, izabraniPlan))}</span> po
              isporuci, umjesto {formatPrice(izabrana.cijena)}.{" "}
              {obavezaPlana(izabrana, izabraniPlan) > 0
                ? `Obaveza je ${isporuka(obavezaPlana(izabrana, izabraniPlan))}; pauza je moguća, a otkaz prije kraja obaveze ide preko podrške.`
                : "Pretplata se može pauzirati ili otkazati u svakom trenutku."}
            </p>
          )}

          {/* Greške oko adrese i tačke stižu pod ključem `fullAddress` — backend za
              pretplatu koristi ista `AddressRules` pravila kao adresar. */}
          <Field label="Adresa dostave" required error={errors.fullAddress?.[0]}>
            <AddressPicker
              id="pretplata-adresa"
              value={form.deliveryAddress}
              onChange={(value) => setForm((f) => ({ ...f, deliveryAddress: value }))}
              onCoordinates={postaviTacku}
              onLocalities={predloziZonu}
              initialCoordinates={
                pretplata?.latitude != null && pretplata?.longitude != null
                  ? { lat: pretplata.latitude, lon: pretplata.longitude }
                  : undefined
              }
            />
          </Field>

          <Field label="Zona dostave" required error={errors.deliveryZoneId?.[0]}>
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

          <Field label="Termin dostave" required error={errors.deliverySlotId?.[0]}>
            <Select
              value={form.deliverySlotId}
              onChange={(e) => setForm({ ...form, deliverySlotId: e.target.value })}
            >
              <option value="">Odaberite…</option>
              {slots.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.label ?? `${s.timeFrom}–${s.timeTo}`}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Napomena za vozača (opcionalno)" error={errors.napomena?.[0]}>
            <Textarea
              rows={2}
              value={form.napomena}
              onChange={(e) => setForm({ ...form, napomena: e.target.value })}
              placeholder="Zvoni dvaput, stan na drugom spratu…"
            />
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
