"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Minus, Plus, Search, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { AddressPicker } from "@/components/shop/address-picker";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { useDeliverySlots, useDeliveryZones, useProducts } from "@/lib/api/hooks";
import { orderApi, korpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { CreateOrderRequest } from "@/lib/api/contracts";
import type { Product } from "@/types/domain";
import { korpicaKaoProizvod } from "@/lib/korpice";
import { formatPrice, unitSuffix, weekdayName } from "@/lib/utils";

export default function AdminNewOrderPage() {
  const router = useRouter();
  const qc = useQueryClient();
  const { show } = useToast();

  const products = useProducts();
  // Javni /products (iznad) ne sadrži korpice (vidi backend GetProductsHandler) — bez
  // ovoga, administrator ne bi mogao ručno dodati korpicu u telefonsku/ličnu narudžbu.
  const korpice = useQuery({
    queryKey: ["korpice", undefined],
    queryFn: () => korpiceApi.lista(),
  });
  const zones = useDeliveryZones();
  const slots = useDeliverySlots();

  const katalog = useMemo(
    () => [...(products.data ?? []), ...(korpice.data ?? []).map(korpicaKaoProizvod)],
    [products.data, korpice.data],
  );

  const [form, setForm] = useState({
    name: "",
    email: "",
    phone: "",
    zoneId: "",
    slotId: "",
    address: "",
    note: "",
  });
  const [qty, setQty] = useState<Record<string, number>>({});
  const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(null);
  const [search, setSearch] = useState("");
  const [errors, setErrors] = useState<Record<string, string[]>>({});

  const set = (k: keyof typeof form) => (v: string) => setForm((f) => ({ ...f, [k]: v }));

  const productById = useMemo(() => {
    const m = new Map<string, Product>();
    katalog.forEach((p) => m.set(p.id, p));
    return m;
  }, [katalog]);

  const lines = useMemo(
    () =>
      Object.entries(qty)
        .map(([id, q]) => ({ product: productById.get(id), quantity: q }))
        .filter((x): x is { product: Product; quantity: number } => !!x.product && x.quantity > 0),
    [qty, productById],
  );

  const selectedZone = useMemo(
    () => zones.data?.find((z) => z.id === form.zoneId),
    [zones.data, form.zoneId],
  );
  const deliveryFee = selectedZone?.deliveryFee ?? 0;
  const subtotal = useMemo(
    () => Math.round(lines.reduce((s, l) => s + l.product.price * l.quantity, 0) * 100) / 100,
    [lines],
  );
  const total = Math.round((subtotal + deliveryFee) * 100) / 100;
  const belowMin = selectedZone ? subtotal < selectedZone.minOrderAmount : false;

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    const avail = katalog.filter((p) => p.isAvailable);
    return (q ? avail.filter((p) => p.name.toLowerCase().includes(q)) : avail).slice(0, 8);
  }, [katalog, search]);

  function bump(id: string, delta: number) {
    setQty((m) => {
      const next = { ...m };
      const q = (next[id] ?? 0) + delta;
      if (q <= 0) delete next[id];
      else next[id] = q;
      return next;
    });
  }

  const mutation = useMutation({
    mutationFn: (body: CreateOrderRequest) => orderApi.create(body),
    onSuccess: (order) => {
      qc.invalidateQueries({ queryKey: ["admin", "orders"] });
      qc.invalidateQueries({ queryKey: ["admin", "dashboard"] });
      show(`Narudžba ${order.code} kreirana`);
      router.push("/admin/orders");
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) setErrors(err.fieldErrors);
    },
  });

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    mutation.mutate({
      customer: { name: form.name, email: form.email, phone: form.phone },
      deliveryZoneId: form.zoneId,
      deliverySlotId: form.slotId,
      deliveryAddress: form.address,
      note: form.note || undefined,
      items: lines.map((l) => ({ productId: l.product.id, quantity: l.quantity })),
      latitude: coords?.lat,
      longitude: coords?.lon,
    });
  }

  const canSubmit =
    !!form.name &&
    !!form.phone &&
    !!form.zoneId &&
    !!form.slotId &&
    !!form.address &&
    lines.length > 0 &&
    !belowMin &&
    !mutation.isPending;

  if (products.isLoading || korpice.isLoading || zones.isLoading || slots.isLoading)
    return <LoadingBlock />;

  return (
    <form onSubmit={submit} className="space-y-6">
      <div className="flex items-center gap-3">
        <Link
          href="/admin/orders"
          className="grid h-9 w-9 shrink-0 place-items-center rounded-lg text-muted transition hover:bg-sand hover:text-ink"
          aria-label="Nazad"
        >
          <ArrowLeft className="h-5 w-5" />
        </Link>
        <div>
          <h1 className="font-display text-2xl font-bold">Nova narudžba</h1>
          <p className="text-muted">Ručni unos narudžbe (telefon / lično).</p>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <div className="space-y-6">
          {/* Kupac */}
          <section className="rounded-card bg-white p-5 shadow-card">
            <h2 className="mb-4 font-display text-lg font-semibold">Kupac</h2>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Ime i prezime" required error={errors["customer.name"]?.[0]}>
                <Input value={form.name} onChange={(e) => set("name")(e.target.value)} />
              </Field>
              <Field label="Telefon" required error={errors["customer.phone"]?.[0]}>
                <Input
                  value={form.phone}
                  onChange={(e) => set("phone")(e.target.value)}
                  placeholder="+387 6x xxx xxx"
                />
              </Field>
              <Field label="Email (opcionalno)" error={errors["customer.email"]?.[0]}>
                <Input
                  type="email"
                  value={form.email}
                  onChange={(e) => set("email")(e.target.value)}
                  placeholder="za potvrdu emailom"
                />
              </Field>
            </div>
          </section>

          {/* Proizvodi */}
          <section className="rounded-card bg-white p-5 shadow-card">
            <h2 className="mb-4 font-display text-lg font-semibold">Proizvodi</h2>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Traži proizvod za dodavanje…"
                className="pl-9"
              />
            </div>
            <div className="mt-3 space-y-1">
              {filtered.map((p) => (
                <button
                  type="button"
                  key={p.id}
                  onClick={() => bump(p.id, 1)}
                  className="flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2 text-left text-sm transition hover:bg-sand"
                >
                  <span className="min-w-0 truncate">{p.name}</span>
                  <span className="flex shrink-0 items-center gap-1 text-muted">
                    {formatPrice(p.price)}/{unitSuffix(p.unit)}
                    <Plus className="h-4 w-4 text-brand-600" />
                  </span>
                </button>
              ))}
            </div>

            {lines.length > 0 && (
              <div className="mt-4 space-y-2 border-t border-line pt-4">
                {lines.map((l) => (
                  <div key={l.product.id} className="flex items-center gap-3">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">{l.product.name}</p>
                      <p className="text-xs text-muted">
                        {formatPrice(l.product.price)} · ukupno{" "}
                        {formatPrice(l.product.price * l.quantity)}
                      </p>
                    </div>
                    <div className="flex items-center gap-1">
                      <button
                        type="button"
                        onClick={() => bump(l.product.id, -1)}
                        className="grid h-8 w-8 place-items-center rounded-lg ring-1 ring-black/10 transition hover:bg-sand"
                        aria-label="Manje"
                      >
                        <Minus className="h-4 w-4" />
                      </button>
                      <span className="w-8 text-center text-sm font-semibold">{l.quantity}</span>
                      <button
                        type="button"
                        onClick={() => bump(l.product.id, 1)}
                        className="grid h-8 w-8 place-items-center rounded-lg ring-1 ring-black/10 transition hover:bg-sand"
                        aria-label="Više"
                      >
                        <Plus className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => bump(l.product.id, -l.quantity)}
                        className="ml-1 grid h-8 w-8 place-items-center rounded-lg text-muted transition hover:bg-red-50 hover:text-red-600"
                        aria-label="Ukloni"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Dostava */}
          <section className="rounded-card bg-white p-5 shadow-card">
            <h2 className="mb-4 font-display text-lg font-semibold">Dostava</h2>
            <div className="grid gap-4">
              <Field label="Zona dostave" required error={errors.deliveryZoneId?.[0]}>
                <Select value={form.zoneId} onChange={(e) => set("zoneId")(e.target.value)}>
                  <option value="">Odaberite zonu…</option>
                  {zones.data?.map((z) => (
                    <option key={z.id} value={z.id}>
                      {z.name} · min {formatPrice(z.minOrderAmount)} · dostava{" "}
                      {formatPrice(z.deliveryFee)}
                    </option>
                  ))}
                </Select>
              </Field>

              <Field label="Adresa" required error={errors.deliveryAddress?.[0]}>
                <AddressPicker
                  value={form.address}
                  onChange={set("address")}
                  onCoordinates={setCoords}
                />
              </Field>

              <Field label="Termin dostave" required error={errors.deliverySlotId?.[0]}>
                <Select value={form.slotId} onChange={(e) => set("slotId")(e.target.value)}>
                  <option value="">Odaberite termin…</option>
                  {slots.data?.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.label ?? `${weekdayName(s.dayOfWeek)} ${s.timeFrom}–${s.timeTo}`}
                    </option>
                  ))}
                </Select>
              </Field>

              <Field label="Napomena (opcionalno)">
                <Textarea
                  value={form.note}
                  onChange={(e) => set("note")(e.target.value)}
                  placeholder="Npr. sprat, interfon, ostaviti kod komšije…"
                />
              </Field>
            </div>
          </section>
        </div>

        {/* Sažetak */}
        <aside className="h-fit rounded-card bg-white p-5 shadow-card lg:sticky lg:top-6">
          <h2 className="font-display text-lg font-semibold">Sažetak</h2>
          {lines.length === 0 ? (
            <p className="mt-3 text-sm text-muted">Dodajte proizvode iznad.</p>
          ) : (
            <ul className="mt-3 space-y-1.5 text-sm">
              {lines.map((l) => (
                <li key={l.product.id} className="flex justify-between gap-2">
                  <span className="min-w-0 truncate text-muted">
                    {l.product.name} × {l.quantity}
                  </span>
                  <span className="shrink-0 font-medium">
                    {formatPrice(l.product.price * l.quantity)}
                  </span>
                </li>
              ))}
            </ul>
          )}

          <dl className="mt-4 space-y-2 border-t border-line pt-4 text-sm">
            <div className="flex justify-between">
              <dt className="text-muted">Međuzbir</dt>
              <dd className="font-medium">{formatPrice(subtotal)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-muted">Dostava</dt>
              <dd className="font-medium">{selectedZone ? formatPrice(deliveryFee) : "—"}</dd>
            </div>
          </dl>

          <div className="mt-4 flex items-center justify-between border-t border-line pt-4">
            <span className="font-semibold">Ukupno</span>
            <span className="font-display text-xl font-bold">{formatPrice(total)}</span>
          </div>

          {belowMin && selectedZone && (
            <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
              Minimalna narudžba za zonu {selectedZone.name} je{" "}
              {formatPrice(selectedZone.minOrderAmount)}.
            </p>
          )}
          {mutation.isError &&
            mutation.error instanceof ApiRequestError &&
            Object.keys(mutation.error.fieldErrors).length === 0 && (
              <p className="mt-3 rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                {mutation.error.message}
              </p>
            )}

          <Button type="submit" size="lg" className="mt-5 w-full" disabled={!canSubmit}>
            {mutation.isPending ? "Kreiram…" : "Kreiraj narudžbu"}
          </Button>
        </aside>
      </div>
    </form>
  );
}
