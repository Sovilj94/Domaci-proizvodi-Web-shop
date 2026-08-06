"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useMutation } from "@tanstack/react-query";
import {
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  ClipboardCheck,
  Mail,
  MapPin,
  ShoppingBasket,
  User,
} from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { AddressChooser } from "@/components/shop/address-chooser";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { StatusBadge } from "@/components/ui/status-badge";
import {
  useDeliverySlots,
  useDeliveryZones,
  useMyAddresses,
  useProducts,
} from "@/lib/api/hooks";
import { orderApi } from "@/lib/api/endpoints";
import type { CreateOrderRequest } from "@/lib/api/contracts";
import { useCart } from "@/lib/cart/cart-context";
import { useAuth } from "@/lib/auth/auth-context";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import type { Order } from "@/types/domain";
import { cn, formatPrice, unitSuffix, weekdayName } from "@/lib/utils";

const STEPS = [
  { id: 1, label: "Adresa", icon: MapPin },
  { id: 2, label: "Podaci", icon: User },
  { id: 3, label: "Potvrda", icon: ClipboardCheck },
] as const;

export default function CheckoutPage() {
  const { items, subtotal, clear, hydrated, reconcile } = useCart();
  const { user } = useAuth();
  const { show } = useToast();
  const zones = useDeliveryZones();
  const slots = useDeliverySlots();
  const products = useProducts();
  const addresses = useMyAddresses(!!user);
  const savedAddresses = useMemo(() => addresses.data ?? [], [addresses.data]);

  // Drop cart items whose product no longer exists / isn't available (e.g. after a
  // catalog change) so checkout can't fail with "proizvod nije dostupan".
  useEffect(() => {
    const list = products.data;
    if (!list?.length) return;
    const available = new Set(list.filter((p) => p.isAvailable).map((p) => p.id));
    const removed = items.filter((i) => !available.has(i.productId)).length;
    reconcile(list);
    if (removed > 0) show(`Uklonjeno ${removed} nedostupnih proizvoda iz korpe.`);
  }, [products.data, items, reconcile, show]);

  const [step, setStep] = useState(1);
  const [form, setForm] = useState({
    name: "",
    email: "",
    phone: "",
    zoneId: "",
    slotId: "",
    address: "",
    note: "",
  });
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [placed, setPlaced] = useState<Order | null>(null);

  // Prefill from the logged-in customer's profile.
  useEffect(() => {
    if (!user) return;
    setForm((f) => ({
      ...f,
      name: f.name || user.name,
      email: f.email || user.email,
      phone: f.phone || user.phone || "",
      zoneId: f.zoneId || user.deliveryZoneId || "",
    }));
  }, [user]);

  // Default the address to the most-recent delivered address (fallback: profile address).
  useEffect(() => {
    const def = savedAddresses[0] ?? user?.address ?? "";
    if (def) setForm((f) => (f.address ? f : { ...f, address: def }));
  }, [savedAddresses, user]);

  const set = (key: keyof typeof form) => (value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  // Best-effort: auto-select the delivery zone from the address's neighbourhood name.
  const applyZoneHint = useCallback(
    (localities: string[]) => {
      const zs = zones.data;
      if (!zs || localities.length === 0) return;
      const match = zs.find((z) =>
        localities.some((l) => {
          const a = l.toLowerCase();
          const b = z.name.toLowerCase();
          return a === b || a.includes(b) || b.includes(a);
        }),
      );
      if (match) setForm((f) => ({ ...f, zoneId: match.id }));
    },
    [zones.data],
  );

  const selectedZone = useMemo(
    () => zones.data?.find((z) => z.id === form.zoneId),
    [zones.data, form.zoneId],
  );
  const deliveryFee = selectedZone?.deliveryFee ?? 0;
  const total = Math.round((subtotal + deliveryFee) * 100) / 100;
  const belowMin = selectedZone ? subtotal < selectedZone.minOrderAmount : false;

  const step1Valid = !!form.address && !!form.zoneId && !!form.slotId;
  const step2Valid = !!form.name && !!form.phone;
  const canOrder = step1Valid && step2Valid && !belowMin && items.length > 0;

  const mutation = useMutation({
    mutationFn: (body: CreateOrderRequest) => orderApi.create(body),
    onSuccess: (order) => {
      clear();
      setPlaced(order);
      window.scrollTo({ top: 0, behavior: "smooth" });
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
      items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
    });
  }

  if (!hydrated) return <LoadingBlock />;

  if (placed) return <Confirmation order={placed} loggedIn={!!user} />;

  if (items.length === 0) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12">
        <EmptyState
          icon={<ShoppingBasket className="h-7 w-7" />}
          title="Korpa je prazna"
          description="Dodajte proizvode prije nego nastavite na dostavu."
          action={
            <Link href="/products" className={cn(buttonVariants())}>
              Pogledaj proizvode
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-2xl px-4 py-8">
      <h1 className="text-center font-display text-2xl font-bold md:text-3xl">
        Dostava i plaćanje
      </h1>

      {/* Stepper */}
      <ol className="mt-6 flex items-center justify-center gap-2">
        {STEPS.map((s, i) => {
          const state = step === s.id ? "current" : step > s.id ? "done" : "todo";
          return (
            <li key={s.id} className="flex items-center gap-2">
              <div
                className={cn(
                  "flex items-center gap-2 rounded-lg px-2.5 py-1.5 text-sm font-medium",
                  state === "current" && "bg-brand-600 text-white",
                  state === "done" && "bg-brand-100 text-brand-700",
                  state === "todo" && "text-muted",
                )}
              >
                <s.icon className="h-4 w-4" />
                <span className="hidden sm:inline">{s.label}</span>
              </div>
              {i < STEPS.length - 1 && (
                <div
                  className={cn("h-px w-8 sm:w-12", step > s.id ? "bg-brand-300" : "bg-black/10")}
                />
              )}
            </li>
          );
        })}
      </ol>

      <form onSubmit={submit} className="mt-6 space-y-6">
        {/* Step 1 — address & delivery */}
        {step === 1 && (
          <section className="space-y-5 rounded-card bg-white p-5 ring-1 ring-black/5">
            <div>
              <h2 className="font-display text-lg font-semibold">Gdje da dostavimo?</h2>
              <p className="text-sm text-muted">
                Upišite adresu i potvrdite lokaciju na mapi.
              </p>
            </div>

            <Field label="Adresa" required error={errors.deliveryAddress?.[0]}>
              <AddressChooser
                value={form.address}
                onChange={set("address")}
                savedAddresses={savedAddresses}
                onLocalities={applyZoneHint}
              />
            </Field>

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

            {belowMin && selectedZone && (
              <p className="rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                Minimalna narudžba za zonu {selectedZone.name} je{" "}
                {formatPrice(selectedZone.minOrderAmount)}.
              </p>
            )}
          </section>
        )}

        {/* Step 2 — contact */}
        {step === 2 && (
          <section className="space-y-4 rounded-card bg-white p-5 ring-1 ring-black/5">
            <div>
              <h2 className="font-display text-lg font-semibold">Vaši podaci</h2>
              <p className="text-sm text-muted">Da vas kontaktiramo za dostavu.</p>
            </div>
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
                placeholder="za potvrdu narudžbe emailom"
              />
            </Field>
          </section>
        )}

        {/* Step 3 — review */}
        {step === 3 && (
          <section className="space-y-5 rounded-card bg-white p-5 ring-1 ring-black/5">
            <h2 className="font-display text-lg font-semibold">Potvrdi narudžbu</h2>

            <ul className="space-y-2 text-sm">
              {items.map((i) => (
                <li key={i.productId} className="flex justify-between gap-2">
                  <span className="min-w-0 truncate text-muted">
                    {i.name} × {i.quantity}
                    {unitSuffix(i.unit)}
                  </span>
                  <span className="shrink-0 font-medium">{formatPrice(i.price * i.quantity)}</span>
                </li>
              ))}
            </ul>

            <dl className="space-y-2 border-t border-black/5 pt-4 text-sm">
              <div className="flex justify-between">
                <dt className="text-muted">Međuzbir</dt>
                <dd className="font-medium">{formatPrice(subtotal)}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted">Dostava</dt>
                <dd className="font-medium">{formatPrice(deliveryFee)}</dd>
              </div>
              <div className="flex justify-between border-t border-black/5 pt-2 text-base">
                <dt className="font-semibold">Ukupno</dt>
                <dd className="font-display font-bold">{formatPrice(total)}</dd>
              </div>
            </dl>

            <div className="rounded-xl bg-cream p-4 text-sm">
              <p className="flex items-start gap-2">
                <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" />
                <span>
                  <span className="font-medium">{form.address}</span>
                  <br />
                  <span className="text-muted">
                    {selectedZone?.name} ·{" "}
                    {slots.data?.find((s) => s.id === form.slotId)?.label ?? "termin"}
                  </span>
                </span>
              </p>
              <p className="mt-2 border-t border-black/5 pt-2 font-medium text-brand-700">
                Plaćanje pouzećem pri dostavi
              </p>
            </div>

            <Field label="Napomena (opcionalno)">
              <Textarea
                value={form.note}
                onChange={(e) => set("note")(e.target.value)}
                placeholder="Npr. sprat, interfon, ostaviti kod komšije…"
              />
            </Field>

            {belowMin && selectedZone && (
              <p className="rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                Minimalna narudžba za zonu {selectedZone.name} je{" "}
                {formatPrice(selectedZone.minOrderAmount)}.
              </p>
            )}
            {mutation.isError &&
              mutation.error instanceof ApiRequestError &&
              Object.keys(mutation.error.fieldErrors).length === 0 && (
                <p className="rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                  {mutation.error.message}
                </p>
              )}
          </section>
        )}

        {/* Bottom navigation */}
        <div className="flex items-center justify-between gap-3">
          {step > 1 ? (
            <Button type="button" variant="secondary" onClick={() => setStep((s) => s - 1)}>
              <ArrowLeft className="h-4 w-4" /> Nazad
            </Button>
          ) : (
            <Link href="/cart" className={cn(buttonVariants({ variant: "secondary" }))}>
              <ArrowLeft className="h-4 w-4" /> Korpa
            </Link>
          )}

          <div className="flex items-center gap-3">
            <span className="font-display text-lg font-bold">
              {formatPrice(selectedZone ? total : subtotal)}
            </span>
            {step < 3 ? (
              <Button
                type="button"
                onClick={() => setStep((s) => s + 1)}
                disabled={(step === 1 && !step1Valid) || (step === 2 && !step2Valid)}
              >
                Nastavi <ArrowRight className="h-4 w-4" />
              </Button>
            ) : (
              <Button type="submit" disabled={!canOrder || mutation.isPending}>
                {mutation.isPending ? "Šaljem…" : "Naruči"}
              </Button>
            )}
          </div>
        </div>
      </form>

      <p className="mt-3 text-center text-xs text-muted">Plaćanje pouzećem pri dostavi.</p>
    </div>
  );
}

function Confirmation({ order, loggedIn }: { order: Order; loggedIn: boolean }) {
  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <div className="rounded-card bg-white p-8 text-center ring-1 ring-black/5">
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-full bg-brand-100 text-brand-600">
          <CheckCircle2 className="h-9 w-9" />
        </div>
        <h1 className="mt-5 font-display text-2xl font-bold">Narudžba primljena!</h1>
        <p className="mt-1 text-muted">
          Broj narudžbe <span className="font-semibold text-ink">{order.code}</span>
        </p>
        <p className="mt-4 inline-flex items-center gap-2 rounded-full bg-honey-100 px-4 py-2 text-sm">
          <Mail className="h-4 w-4 text-honey-600" /> Potvrda je poslana na email.
        </p>

        <div className="mt-6 space-y-2 rounded-xl bg-cream p-4 text-left text-sm">
          <div className="flex justify-between">
            <span className="text-muted">Status</span>
            <StatusBadge status={order.status} />
          </div>
          <div className="flex justify-between">
            <span className="text-muted">Dostava</span>
            <span className="font-medium">
              {order.deliverySlot?.label} · {order.deliveryZoneName}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted">Adresa</span>
            <span className="max-w-[60%] text-right font-medium">{order.deliveryAddress}</span>
          </div>
          <div className="flex justify-between border-t border-black/5 pt-2">
            <span className="font-semibold">Ukupno</span>
            <span className="font-bold">{formatPrice(order.totalAmount)}</span>
          </div>
        </div>

        <div className="mt-7 flex flex-wrap justify-center gap-3">
          <Link href="/products" className={cn(buttonVariants({ variant: "secondary" }))}>
            Nastavi kupovinu
          </Link>
          {loggedIn && (
            <Link href="/orders" className={cn(buttonVariants())}>
              Moje narudžbe
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
