"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus } from "lucide-react";
import { AdminLadica } from "@/components/admin/admin-ladica";
import { Button } from "@/components/ui/button";
import { Field, Input, Select } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { cn, formatPrice, weekdayName } from "@/lib/utils";
import type { DeliverySlot, DeliveryZone } from "@/types/domain";

/**
 * Zone i termini dostave. Zona se ne briše (narudžbe je pamte) nego gasi. Termin sa najranijim
 * danom u sedmici određuje rok naručivanja za cijelu sedmicu — zato upozorenje na formi.
 */
export default function AdminZonePage() {
  const zone = useQuery({ queryKey: ["admin", "zone"], queryFn: () => adminApi.zone() });
  const termini = useQuery({ queryKey: ["admin", "termini"], queryFn: () => adminApi.termini() });
  const [zona, setZona] = useState<DeliveryZone | "nova" | null>(null);
  const [termin, setTermin] = useState<DeliverySlot | "novi" | null>(null);

  const prviDan = termini.data?.length ? Math.min(...termini.data.map((t) => t.dayOfWeek)) : null;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-display text-2xl font-bold text-ink">Zone i termini dostave</h1>
        <p className="text-sm text-muted">Cijena dostave i minimum po zoni, dani i sati isporuke, rok naručivanja.</p>
      </div>

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-display text-lg font-bold text-ink">Zone</h2>
          <Button size="sm" onClick={() => setZona("nova")}>
            <Plus className="h-4 w-4" /> Nova zona
          </Button>
        </div>
        {zone.isLoading ? (
          <LoadingBlock />
        ) : (
          <ul className="divide-y divide-line rounded-card border border-line bg-surface">
            {zone.data?.map((z) => (
              <li key={z.id}>
                <button
                  type="button"
                  onClick={() => setZona(z)}
                  className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left text-sm hover:bg-sand"
                >
                  <span className={cn("font-medium", !z.isActive && "text-muted line-through")}>{z.name}</span>
                  <span className="text-muted">
                    min {formatPrice(z.minOrderAmount)} · dostava {formatPrice(z.deliveryFee)}
                    {!z.isActive && " · ugašena"}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="font-display text-lg font-bold text-ink">Termini isporuke</h2>
          <Button size="sm" onClick={() => setTermin("novi")}>
            <Plus className="h-4 w-4" /> Novi termin
          </Button>
        </div>
        {termini.isLoading ? (
          <LoadingBlock />
        ) : (
          <ul className="divide-y divide-line rounded-card border border-line bg-surface">
            {termini.data?.map((t) => (
              <li key={t.id}>
                <button
                  type="button"
                  onClick={() => setTermin(t)}
                  className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left text-sm hover:bg-sand"
                >
                  <span className="font-medium">
                    {weekdayName(t.dayOfWeek)} {t.timeFrom}–{t.timeTo}
                    {t.dayOfWeek === prviDan && (
                      <span className="ml-2 rounded-full bg-honey-100 px-2 py-0.5 text-xs text-honey-700">određuje rok</span>
                    )}
                  </span>
                  <span className="text-muted">rok {t.cutoffHours} h prije</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      {zona && <ZonaForma zona={zona === "nova" ? null : zona} onClose={() => setZona(null)} />}
      {termin && <TerminForma termin={termin === "novi" ? null : termin} onClose={() => setTermin(null)} />}
    </div>
  );
}

function useSacuvaj<T>(fn: () => Promise<T>, kljuc: string, poruka: string, onClose: () => void) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [greske, setGreske] = useState<Record<string, string[]>>({});
  const m = useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", kljuc] });
      show(poruka);
      onClose();
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) {
        setGreske(err.fieldErrors);
        if (Object.keys(err.fieldErrors).length === 0) show(err.message, "error");
        return;
      }
      show("Čuvanje nije uspjelo", "error");
    },
  });
  return { m, greske, setGreske };
}

function ZonaForma({ zona, onClose }: { zona: DeliveryZone | null; onClose: () => void }) {
  const [f, setF] = useState({
    name: zona?.name ?? "",
    minOrderAmount: String(zona?.minOrderAmount ?? 0),
    deliveryFee: String(zona?.deliveryFee ?? 0),
    isActive: zona?.isActive ?? true,
  });
  const { m, greske, setGreske } = useSacuvaj(
    () =>
      adminApi.sacuvajZonu(zona?.id ?? null, {
        name: f.name,
        minOrderAmount: Number(f.minOrderAmount) || 0,
        deliveryFee: Number(f.deliveryFee) || 0,
        isActive: f.isActive,
      }),
    "zone",
    zona ? "Zona sačuvana" : "Zona dodana",
    onClose,
  );

  return (
    <AdminLadica naslov={zona ? "Uredi zonu" : "Nova zona"} onClose={onClose}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setGreske({});
          m.mutate();
        }}
        className="flex-1 space-y-4 overflow-y-auto p-6"
      >
        <Field label="Naziv" required error={greske["input.name"]?.[0]}>
          <Input value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} maxLength={80} />
        </Field>
        <Field label="Minimalna narudžba (KM)" error={greske["input.minOrderAmount"]?.[0]}>
          <Input type="number" min={0} step="0.5" value={f.minOrderAmount} onChange={(e) => setF({ ...f, minOrderAmount: e.target.value })} />
        </Field>
        <Field label="Cijena dostave (KM)" error={greske["input.deliveryFee"]?.[0]}>
          <Input type="number" min={0} step="0.5" value={f.deliveryFee} onChange={(e) => setF({ ...f, deliveryFee: e.target.value })} />
        </Field>
        <label className="flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={f.isActive} onChange={(e) => setF({ ...f, isActive: e.target.checked })} className="h-4 w-4 accent-brand-600" />
          Aktivna (kupci je mogu izabrati)
        </label>
        <Button type="submit" className="w-full" disabled={m.isPending}>
          {m.isPending ? "Čuvam…" : "Sačuvaj"}
        </Button>
      </form>
    </AdminLadica>
  );
}

const DANI = [1, 2, 3, 4, 5, 6, 0];

function TerminForma({ termin, onClose }: { termin: DeliverySlot | null; onClose: () => void }) {
  const qc = useQueryClient();
  const { show } = useToast();
  const [f, setF] = useState({
    dayOfWeek: String(termin?.dayOfWeek ?? 4),
    timeFrom: termin?.timeFrom ?? "16:00",
    timeTo: termin?.timeTo ?? "20:00",
    cutoffHours: String(termin?.cutoffHours ?? 48),
    label: termin?.label ?? "",
  });
  const { m, greske, setGreske } = useSacuvaj(
    () =>
      adminApi.sacuvajTermin(termin?.id ?? null, {
        dayOfWeek: Number(f.dayOfWeek),
        timeFrom: f.timeFrom,
        timeTo: f.timeTo,
        cutoffHours: Math.trunc(Number(f.cutoffHours)) || 0,
        label: f.label || undefined,
      }),
    "termini",
    termin ? "Termin sačuvan" : "Termin dodan",
    onClose,
  );
  const brisanje = useMutation({
    mutationFn: () => adminApi.obrisiTermin(termin!.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["admin", "termini"] });
      show("Termin obrisan");
      onClose();
    },
    onError: (err) => show(err instanceof ApiRequestError ? err.message : "Brisanje nije uspjelo", "error"),
  });
  const [potvrda, setPotvrda] = useState(false);

  return (
    <AdminLadica naslov={termin ? "Uredi termin" : "Novi termin"} onClose={onClose}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          setGreske({});
          m.mutate();
        }}
        className="flex-1 space-y-4 overflow-y-auto p-6"
      >
        <p className="rounded-lg bg-honey-100 px-3 py-2 text-sm text-honey-700">
          Termin sa najranijim danom u sedmici određuje rok naručivanja za sve. Promjena dana, sata ili roka pomjera rok
          svim kupcima i pretplatama.
        </p>
        <Field label="Dan" error={greske["input.dayOfWeek"]?.[0]}>
          <Select value={f.dayOfWeek} onChange={(e) => setF({ ...f, dayOfWeek: e.target.value })}>
            {DANI.map((d) => (
              <option key={d} value={d}>
                {weekdayName(d)}
              </option>
            ))}
          </Select>
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Od" error={greske["input.timeFrom"]?.[0]}>
            <Input type="time" value={f.timeFrom} onChange={(e) => setF({ ...f, timeFrom: e.target.value })} />
          </Field>
          <Field label="Do" error={greske["input.timeTo"]?.[0]}>
            <Input type="time" value={f.timeTo} onChange={(e) => setF({ ...f, timeTo: e.target.value })} />
          </Field>
        </div>
        <Field label="Rok naručivanja (sati prije početka isporuke)" error={greske["input.cutoffHours"]?.[0]}>
          <Input type="number" min={0} max={168} value={f.cutoffHours} onChange={(e) => setF({ ...f, cutoffHours: e.target.value })} />
        </Field>
        <Field label="Oznaka (opcionalno)" error={greske["input.label"]?.[0]}>
          <Input value={f.label} onChange={(e) => setF({ ...f, label: e.target.value })} maxLength={60} placeholder="npr. Četvrtak popodne" />
        </Field>
        <Button type="submit" className="w-full" disabled={m.isPending}>
          {m.isPending ? "Čuvam…" : "Sačuvaj"}
        </Button>
        {termin &&
          (potvrda ? (
            <div className="flex gap-2">
              <Button type="button" variant="danger" className="flex-1" disabled={brisanje.isPending} onClick={() => brisanje.mutate()}>
                Potvrdi brisanje
              </Button>
              <Button type="button" variant="ghost" onClick={() => setPotvrda(false)}>
                Odustani
              </Button>
            </div>
          ) : (
            <Button type="button" variant="ghost" className="w-full text-red-700" onClick={() => setPotvrda(true)}>
              Obriši termin
            </Button>
          ))}
      </form>
    </AdminLadica>
  );
}
