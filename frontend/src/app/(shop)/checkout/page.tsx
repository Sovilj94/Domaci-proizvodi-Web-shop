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
import { OrderSummaryPanel } from "@/components/shop/order-summary-panel";
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
import { cn, formatPrice, weekdayName } from "@/lib/utils";

/** Strip a "+387" a saved profile phone may already carry — the field below only holds
 * the local number, since the dial code is now fixed UI, not something the user types. */
function stripDialCode(phone: string): string {
  return phone.replace(/^\+387\s*/, "").trim();
}

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
    ime: "",
    prezime: "",
    email: "",
    phone: "",
    zoneId: "",
    slotId: "",
    ulica: "",
    broj: "",
    stan: "",
    note: "",
  });
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [placed, setPlaced] = useState<Order | null>(null);
  const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(null);

  // Prefill from the logged-in customer's profile. `user.name` is one string on the
  // backend, so it's split on the first space — the rest (middle/last names) all goes
  // into prezime rather than being dropped.
  useEffect(() => {
    if (!user) return;
    const [prvo, ...ostalo] = user.name.trim().split(/\s+/);
    setForm((f) => ({
      ...f,
      ime: f.ime || prvo || "",
      prezime: f.prezime || ostalo.join(" "),
      email: f.email || user.email,
      phone: f.phone || stripDialCode(user.phone ?? ""),
      zoneId: f.zoneId || user.deliveryZoneId || "",
    }));
  }, [user]);

  // Default the address to the most-recent delivered address (fallback: profile address).
  // These are historical full strings (house number already baked in), so they land
  // whole in `ulica` — see `adresaVecPotpuna` below for why that's still valid.
  useEffect(() => {
    const def = savedAddresses[0] ?? user?.address ?? "";
    if (def) setForm((f) => (f.ulica ? f : { ...f, ulica: def }));
  }, [savedAddresses, user]);

  const set = (key: keyof typeof form) => (value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  // AddressChooser's onChange fires both for free typing and for picking one of the
  // customer's past full addresses. The latter already has the house number baked in,
  // so a stale `broj`/`stan` left over from an earlier "Nova adresa" attempt must not
  // get appended to it — that would silently move the pin off the selected address.
  const handleUlicaChange = (value: string) => {
    if (savedAddresses.includes(value)) {
      setForm((f) => ({ ...f, ulica: value, broj: "", stan: "" }));
    } else {
      set("ulica")(value);
    }
  };

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

  // Backend i dalje prima jedno ime i jednu adresu — admin, dispečerski ekran i
  // vozačka aplikacija čitaju upravo to. Razdvojena polja su samo oblik forme.
  const punoIme = [form.ime, form.prezime].filter(Boolean).join(" ").trim();
  const ulicaIBroj = [form.ulica, form.broj].filter(Boolean).join(" ").trim();
  const punaAdresa = form.stan ? `${ulicaIBroj}, stan ${form.stan}` : ulicaIBroj;
  // Prefiks je fiksna UI odluka (dostavljamo samo u BiH) — korisnik kuca samo lokalni
  // broj, pa ga spajamo sa prefiksom tek ovdje, prije slanja.
  const telefon = form.phone.trim() ? `+387 ${form.phone.trim()}` : "";

  // `ulica` je već potpuna adresa (kućni broj uključen) kad dolazi iz istorije narudžbi
  // ili sa profila — tražiti od korisnika da opet upiše broj bi ga blokiralo na koraku
  // 1 iako je adresa već ispravna i geokodirana.
  const adresaVecPotpuna =
    !!form.ulica && (savedAddresses.includes(form.ulica) || form.ulica === user?.address);

  const gost = !user;
  const step1Valid =
    !!form.ulica && (adresaVecPotpuna || !!form.broj) && !!form.zoneId && !!form.slotId;
  const step2Valid =
    !!form.ime && !!form.prezime && !!form.phone && (!gost || !!form.email);
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
      customer: { name: punoIme, email: form.email, phone: telefon },
      deliveryZoneId: form.zoneId,
      deliverySlotId: form.slotId,
      deliveryAddress: punaAdresa,
      note: form.note || undefined,
      items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
      latitude: coords?.lat,
      longitude: coords?.lon,
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
    <div className="mx-auto max-w-2xl px-4 py-8 lg:max-w-5xl">
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

      <div className="mt-6 lg:grid lg:grid-cols-[minmax(0,1fr)_320px] lg:items-start lg:gap-8">
        <form onSubmit={submit} className="space-y-6">
          {/* Step 1 — address & delivery */}
          {step === 1 && (
            <section className="space-y-5 rounded-card bg-surface p-5 shadow-card">
              <div>
                <h2 className="font-display text-lg font-semibold">Gdje da dostavimo?</h2>
                <p className="text-sm text-muted">
                  Upišite adresu i potvrdite lokaciju na mapi.
                </p>
              </div>

              <Field label="Ulica" required error={errors.deliveryAddress?.[0]}>
                <AddressChooser
                  value={ulicaIBroj}
                  onChange={handleUlicaChange}
                  savedAddresses={savedAddresses}
                  onLocalities={applyZoneHint}
                  onCoordinates={setCoords}
                />
              </Field>

              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Kućni broj" required={!adresaVecPotpuna}>
                  <Input
                    value={form.broj}
                    onChange={(e) => set("broj")(e.target.value)}
                    placeholder="npr. 12"
                  />
                </Field>
                <Field label="Stan (opcionalno)">
                  <Input
                    value={form.stan}
                    onChange={(e) => set("stan")(e.target.value)}
                    placeholder="npr. 3"
                  />
                </Field>
              </div>

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
            <section className="space-y-4 rounded-card bg-surface p-5 shadow-card">
              <div>
                <h2 className="font-display text-lg font-semibold">Vaši podaci</h2>
                <p className="text-sm text-muted">Da vas kontaktiramo za dostavu.</p>
              </div>

              <div className="grid gap-4 sm:grid-cols-2">
                <Field label="Ime" required>
                  <Input value={form.ime} onChange={(e) => set("ime")(e.target.value)} />
                </Field>
                <Field label="Prezime" required>
                  <Input value={form.prezime} onChange={(e) => set("prezime")(e.target.value)} />
                </Field>
              </div>
              {errors["customer.name"]?.[0] && (
                <p className="text-xs font-medium text-red-600">{errors["customer.name"][0]}</p>
              )}

              <Field label="Telefon" required error={errors["customer.phone"]?.[0]}>
                <div className="flex items-stretch">
                  <span className="flex shrink-0 items-center rounded-l-xl border border-r-0 border-line bg-sand px-3.5 text-sm font-medium text-muted">
                    +387
                  </span>
                  <Input
                    value={form.phone}
                    onChange={(e) => set("phone")(e.target.value)}
                    placeholder="6x xxx xxx"
                    className="min-w-0 flex-1 rounded-l-none"
                  />
                </div>
              </Field>

              <Field
                label={gost ? "Email" : "Email (opcionalno)"}
                required={gost}
                error={errors["customer.email"]?.[0]}
              >
                <Input
                  type="email"
                  value={form.email}
                  onChange={(e) => set("email")(e.target.value)}
                  placeholder="ime@example.com"
                />
                <p className="mt-1.5 text-xs text-muted">
                  Na ovu adresu šaljemo link za potvrdu narudžbe.
                </p>
              </Field>

              <Field label="Napomena za dostavljača (opcionalno)">
                <Textarea
                  value={form.note}
                  maxLength={237}
                  onChange={(e) => set("note")(e.target.value)}
                  placeholder="Npr. sprat, interfon, ostaviti kod komšije…"
                />
                <p className="mt-1 text-right text-xs text-muted">{form.note.length}/237</p>
              </Field>
            </section>
          )}

          {/* Step 3 — review */}
          {step === 3 && (
            <section className="space-y-5 rounded-card bg-surface p-5 shadow-card">
              <h2 className="font-display text-lg font-semibold">Potvrdi narudžbu</h2>

              <div className="rounded-xl bg-cream p-4 text-sm">
                <p className="flex items-start gap-2">
                  <MapPin className="mt-0.5 h-4 w-4 shrink-0 text-brand-600" />
                  <span>
                    <span className="font-medium">{punaAdresa}</span>
                    <br />
                    <span className="text-muted">
                      {selectedZone?.name} ·{" "}
                      {slots.data?.find((s) => s.id === form.slotId)?.label ?? "termin"}
                    </span>
                  </span>
                </p>
                <p className="mt-2 border-t border-line pt-2 font-medium text-brand-700">
                  Plaćanje pouzećem pri dostavi
                </p>
              </div>

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

          {/* On narrow screens the summary sits right above the buttons; on wide screens
              it lives in the sticky sidebar instead (rendered once below). */}
          <div className="lg:hidden">
            <OrderSummaryPanel
              items={items}
              subtotal={subtotal}
              deliveryFee={deliveryFee}
              total={total}
              zoneName={selectedZone?.name}
            />
          </div>

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

        <aside className="hidden lg:sticky lg:top-24 lg:block">
          <OrderSummaryPanel
            items={items}
            subtotal={subtotal}
            deliveryFee={deliveryFee}
            total={total}
            zoneName={selectedZone?.name}
          />
        </aside>
      </div>

      <p className="mt-3 text-center text-xs text-muted">Plaćanje pouzećem pri dostavi.</p>
    </div>
  );
}

function Confirmation({ order, loggedIn }: { order: Order; loggedIn: boolean }) {
  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <div className="rounded-card bg-surface p-8 text-center shadow-card">
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
          <div className="flex justify-between border-t border-line pt-2">
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
