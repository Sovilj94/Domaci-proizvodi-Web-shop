"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  ArrowRight,
  ClipboardCheck,
  MapPin,
  ShoppingBasket,
  User,
} from "lucide-react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Field, Input, Select, Textarea } from "@/components/ui/field";
import { AddressChooser } from "@/components/shop/address-chooser";
import { CheckEmail } from "@/components/shop/check-email";
import { OrderReceipt } from "@/components/shop/order-receipt";
import { OrderSummaryPanel } from "@/components/shop/order-summary-panel";
import { PromoKodPolje } from "@/components/shop/promo-kod-polje";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import {
  useDeliverySlots,
  useDeliveryZones,
  useKorpiceList,
  useMojiPoeni,
  useMyAddresses,
  useProducts,
  queryKeys,
} from "@/lib/api/hooks";
import { orderApi, promoApi } from "@/lib/api/endpoints";
import type { CreateOrderRequest } from "@/lib/api/contracts";
import { pogodiZonu } from "@/lib/zone";
import { useCart, type CartItem } from "@/lib/cart/cart-context";
import { useAuth } from "@/lib/auth/auth-context";
import { useToast } from "@/components/ui/toast";
import { ApiRequestError } from "@/lib/api/client";
import type { Order, Product } from "@/types/domain";
import { spojiKatalog } from "@/lib/korpice";
import { cn, formatPrice, weekdayName } from "@/lib/utils";

/** Strip a "+387" a saved profile phone may already carry — the field below only holds
 * the local number, since the dial code is now fixed UI, not something the user types. */
function stripDialCode(phone: string): string {
  return phone.replace(/^\+387\s*/, "").trim();
}

/**
 * Privremeni placeholder za stavku korpe koja (još) nije u `products.data` — koristi se
 * samo dok `/korpice` ne stigne, da `reconcile` ne obriše korpicu samo zato što njena
 * stvarna dostupnost još nije poznata. `isAvailable: true` je namjerno — ovo nije tvrdnja
 * da je stavka dostupna, nego "ne gasi je dok ne znamo"; vidi poziv u reconcile efektu
 * ispod, gdje se ovo koristi samo za stavke odsutne iz `products.data` (CartItem ne nosi
 * Vrsta, pa se korpica od stvarno obrisanog proizvoda ne može razlikovati bez korpice liste).
 */
function stavkaKaoPlaceholder(item: CartItem): Product {
  return {
    id: item.productId,
    name: item.name,
    slug: item.slug,
    price: item.price,
    unit: item.unit,
    stock: item.stock,
    isAvailable: true,
    categoryId: "",
    categoryName: "",
    categorySlug: "",
    producerId: "",
    imageUrl: item.imageUrl,
    description: "",
  };
}

const STEPS = [
  { id: 1, label: "Adresa", icon: MapPin },
  { id: 2, label: "Podaci", icon: User },
  { id: 3, label: "Potvrda", icon: ClipboardCheck },
] as const;

export default function CheckoutPage() {
  const { items, subtotal, clear, hydrated, reconcile } = useCart();
  const { user } = useAuth();
  const qc = useQueryClient();
  const { show } = useToast();
  const zones = useDeliveryZones();
  const slots = useDeliverySlots();
  const products = useProducts();
  // Korpice su Product redovi (Vrsta == Korpica), ali se javni katalog (/products) od
  // njih čisti (vidi backend GetProductsHandler) — bez ovoga bi reconcile ispod, koji
  // briše iz korpe sve što nije u listi koju dobije, obrisao svaku korpicu iz korpe
  // čim kupac stigne na checkout. Zato se ovdje dovlače posebno i spajaju s products.
  const korpice = useKorpiceList();
  const addresses = useMyAddresses(!!user);
  const savedAddresses = useMemo(() => addresses.data ?? [], [addresses.data]);

  // Drop cart items whose product no longer exists / isn't available (e.g. after a
  // catalog change) so checkout can't fail with "proizvod nije dostupan".
  //
  // Samo stavke koje su ODSUTNE iz products.data su dvosmislene — mogu biti korpica
  // koja čeka svoju listu, ili stvarno obrisan proizvod; CartItem ne nosi Vrsta pa se ta
  // dva slučaja ne mogu razlikovati bez liste korpica. Stavke koje SU u products.data (sa
  // isAvailable === false) nisu dvosmislene — one se briše odmah, bez obzira da li su
  // korpice stigle, jer čekanje na /korpice ne bi tu ništa razjasnilo, samo bi bespotrebno
  // odgodilo brisanje stvarno rasprodatog običnog proizvoda (i toast o tome).
  //
  // Zato: odsutne stavke se privremeno "štite" placeholderom (stavkaKaoPlaceholder, uvijek
  // isAvailable: true) dok korpice ne stignu uspješno — tada se zamjenjuju stvarnim
  // korpice podacima i konačno presuđuju. Ako zahtjev za korpice trajno padne, odsutne
  // stavke ostaju zaštićene (nikad se ne brišu po placeholderu), ali prisutne-nedostupne i
  // dalje bivaju uklonjene — dostupnost se svakako ponovo provjerava na serveru pri slanju
  // narudžbe, pa trajno čekanje ovdje nije opasno po ispravnost, samo po UX koji se ovim
  // popravlja.
  useEffect(() => {
    const list = products.data;
    if (!list?.length) return;

    const poznatiId = new Set(list.map((p) => p.id));
    const odsutne = items.filter((i) => !poznatiId.has(i.productId));

    const katalog = korpice.isSuccess
      ? spojiKatalog(list, korpice.data)
      : [...list, ...odsutne.map(stavkaKaoPlaceholder)];

    const available = new Set(katalog.filter((p) => p.isAvailable).map((p) => p.id));
    const removed = items.filter((i) => !available.has(i.productId)).length;
    reconcile(katalog);
    if (removed > 0) show(`Uklonjeno ${removed} nedostupnih proizvoda iz korpe.`);
  }, [products.data, korpice.isSuccess, korpice.data, items, reconcile, show]);

  const [step, setStep] = useState(1);
  const [promoKod, setPromoKod] = useState<string | null>(null);
  const [koristiPoene, setKoristiPoene] = useState(false);
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
  const [sentTo, setSentTo] = useState<{ email: string; expiresAt: string } | null>(null);
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

  // AddressChooser's onChange fires for free typing, for picking one of the customer's
  // past full addresses, and (via its own "Nova adresa" button, or the field being
  // cleared by hand) for resetting to a blank slate. Whenever the address *source*
  // changes away from "user is editing this specific street", a stale `broj`/`stan`
  // left over from before must not survive — either would silently graft an old house
  // number onto whatever comes next.
  const handleUlicaChange = (value: string) => {
    if (savedAddresses.includes(value) || value === "") {
      setForm((f) => ({ ...f, ulica: value, broj: "", stan: "" }));
    } else {
      set("ulica")(value);
    }
  };

  // Best-effort: auto-select the delivery zone from the address's neighbourhood name.
  // Sam uslov podudaranja stoji u `lib/zone.ts` jer ga od P2c dijeli sa formom adresara.
  const applyZoneHint = useCallback(
    (localities: string[]) => {
      const match = pogodiZonu(localities, zones.data);
      if (match) setForm((f) => ({ ...f, zoneId: match.id }));
    },
    [zones.data],
  );

  const selectedZone = useMemo(
    () => zones.data?.find((z) => z.id === form.zoneId),
    [zones.data, form.zoneId],
  );
  const deliveryFee = selectedZone?.deliveryFee ?? 0;
  // Popust računa server (provjera koda) — klijent ga samo prikazuje.
  // Provjera ide tek na koraku 3 (e-mail je tada gotov, ne po svakom tasteru); prijavljenom
  // se e-mail ne šalje jer server koristi nalog. Keš i bez refetch-a na fokus štede
  // dijeljeni "auth" limit (10/min).
  const promoEmail = user ? undefined : form.email.trim() || undefined;
  const promo = useQuery({
    queryKey: ["promo", promoKod, promoEmail, items.map((i) => [i.productId, i.quantity])],
    queryFn: () =>
      promoApi.provjeri({
        kod: promoKod!,
        email: promoEmail,
        items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
      }),
    enabled: !!promoKod && items.length > 0 && step === 3,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
  const popust = promo.data?.popust ?? 0;
  // Poeni (samo prijavljeni). Ista formula kao server (`PoeniPravila.ZaTrosenje`): bez poena ili
  // robe nema popusta; popust je cijeli broj KM, najviše koliko poeni vrijede, a ne više od robe
  // (subtotal − promo popust). Poena se troši tačno koliko pokriva taj popust.
  const poeni = useMojiPoeni(!!user);
  const stanjePoena = poeni.data?.stanje ?? 0;
  const vrijednostPoena = poeni.data?.vrijednostPoenaKM ?? 0;
  const roba = subtotal - popust;
  const popustPoena =
    stanjePoena > 0 && roba > 0 && vrijednostPoena > 0
      ? Math.floor(Math.min(stanjePoena * vrijednostPoena, roba))
      : 0;
  const poenaZaTrosenje = vrijednostPoena > 0 ? Math.round(popustPoena / vrijednostPoena) : 0;
  const poeniPopust = koristiPoene ? popustPoena : 0;
  const total = Math.round((subtotal - popust - poeniPopust + deliveryFee) * 100) / 100;
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
  // ili sa profila — tražiti od korisnika da opet upiše broj bi ga blokiralo na koraku 1
  // iako je adresa već ispravna i geokodirana. Ali "potpuna" znači samo da je NEPRAZNA
  // ("required" na registraciji), ne da sadrži broj — zato ovdje dodatno provjeravamo da
  // tekst ima bar jednu cifru; bez toga bi adresa bez broja (npr. samo naziv ulice sa
  // profila) prošla korak 1 a da broj kuće nikad nije prikupljen.
  const adresaVecPotpuna =
    !!form.ulica &&
    (savedAddresses.includes(form.ulica) || form.ulica === user?.address) &&
    /\d/.test(form.ulica);

  const gost = !user;
  const step1Valid =
    !!form.ulica && (adresaVecPotpuna || !!form.broj) && !!form.zoneId && !!form.slotId;
  const step2Valid =
    !!form.ime && !!form.prezime && !!form.phone && (!gost || !!form.email);
  const canOrder = step1Valid && step2Valid && !belowMin && items.length > 0;

  // Logged-in customer: order is created immediately, as before.
  const mutation = useMutation({
    mutationFn: (body: CreateOrderRequest) => orderApi.create(body),
    onSuccess: (order) => {
      clear();
      qc.invalidateQueries({ queryKey: queryKeys.mojiPoeni() });
      setPlaced(order);
      window.scrollTo({ top: 0, behavior: "smooth" });
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) setErrors(err.fieldErrors);
      // Server je odbio kod: skini ga, da zastareli popust nestane i kod se ne šalje ponovo.
      if (err instanceof ApiRequestError && err.apiError.code === "promo_kod_nevazeci") {
        setPromoKod(null);
      }
    },
  });

  // Gost: nema naloga, pa nema kome pripisati narudžbu bez potvrde da je mejl zaista
  // njihov — backend samo rezerviše zahtjev i vraća rok za potvrdu, ne narudžbu.
  const guestMutation = useMutation({
    mutationFn: (body: CreateOrderRequest) => orderApi.guestRequest(body),
    onSuccess: (res) => {
      // Korpa se NE čisti — narudžba još ne postoji, a ako potvrda istekne
      // kupac mora imati čemu da se vrati.
      setSentTo({ email: form.email, expiresAt: res.expiresAt });
      window.scrollTo({ top: 0, behavior: "smooth" });
    },
    onError: (err) => {
      if (err instanceof ApiRequestError) setErrors(err.fieldErrors);
      // Server je odbio kod: skini ga, da zastareli popust nestane i kod se ne šalje ponovo.
      if (err instanceof ApiRequestError && err.apiError.code === "promo_kod_nevazeci") {
        setPromoKod(null);
      }
    },
  });

  const activeMutation = gost ? guestMutation : mutation;

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    const body: CreateOrderRequest = {
      customer: { name: punoIme, email: form.email, phone: telefon },
      deliveryZoneId: form.zoneId,
      deliverySlotId: form.slotId,
      deliveryAddress: punaAdresa,
      note: form.note || undefined,
      items: items.map((i) => ({ productId: i.productId, quantity: i.quantity })),
      latitude: coords?.lat,
      longitude: coords?.lon,
      // Kod koji je server odbio (promo.isError) se ne šalje i ne blokira narudžbu.
      promoKod: promo.data ? promoKod ?? undefined : undefined,
    };
    if (gost) {
      guestMutation.mutate(body);
    } else {
      mutation.mutate({ ...body, iskoristiPoene: koristiPoene && popustPoena > 0 });
    }
  }

  if (!hydrated) return <LoadingBlock />;

  if (sentTo) return <CheckEmail email={sentTo.email} expiresAt={sentTo.expiresAt} />;

  if (placed) return <OrderReceipt order={placed} mode="zahvala" />;

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
                  value={form.ulica}
                  onChange={handleUlicaChange}
                  savedAddresses={savedAddresses}
                  onLocalities={applyZoneHint}
                  onCoordinates={setCoords}
                  querySuffix={form.broj}
                />
              </Field>

              {adresaVecPotpuna ? (
                <p className="text-xs text-muted">
                  Kućni broj i stan su već dio odabrane adrese.
                </p>
              ) : (
                <div className="grid gap-4 sm:grid-cols-2">
                  <Field label="Kućni broj" required>
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
              )}

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

              <PromoKodPolje
                primijenjen={promoKod}
                provjera={promo}
                onPrimijeni={(kod) => {
                  activeMutation.reset();
                  setPromoKod(kod);
                }}
                onUkloni={() => {
                  activeMutation.reset();
                  setPromoKod(null);
                }}
                greskaSlanja={
                  activeMutation.error instanceof ApiRequestError &&
                  activeMutation.error.apiError.code === "promo_kod_nevazeci"
                    ? activeMutation.error.message
                    : undefined
                }
              />

              {user && stanjePoena > 0 && (
                popustPoena > 0 ? (
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={koristiPoene}
                      onChange={(e) => {
                        activeMutation.reset();
                        setKoristiPoene(e.target.checked);
                      }}
                      className="h-4 w-4 accent-brand-600"
                    />
                    <span>
                      Iskoristi poene ({poenaZaTrosenje} poena = −{formatPrice(popustPoena)})
                    </span>
                  </label>
                ) : (
                  <p className="text-sm text-muted">
                    Imaš {stanjePoena} poena — treba ti bar {Math.round(1 / vrijednostPoena)} za
                    popust od 1 KM.
                  </p>
                )
              )}

              {belowMin && selectedZone && (
                <p className="rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                  Minimalna narudžba za zonu {selectedZone.name} je{" "}
                  {formatPrice(selectedZone.minOrderAmount)}.
                </p>
              )}
              {activeMutation.isError &&
                activeMutation.error instanceof ApiRequestError &&
                activeMutation.error.apiError.code !== "promo_kod_nevazeci" &&
                Object.keys(activeMutation.error.fieldErrors).length === 0 && (
                  <p className="rounded-lg bg-red-50 px-3 py-2 text-xs font-medium text-red-700">
                    {activeMutation.error.message}
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
              popust={popust}
              promoKod={promo.data?.kod}
              poeniPopust={poeniPopust}
              iskoristeniPoeni={poenaZaTrosenje}
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
              {/* Različiti `key`-evi su obavezni. Bez njih React zadrži isti <button> i samo mu
                  promijeni `type` u "submit" — a to se desi usred klika na „Nastavi" u koraku 2,
                  prije nego pregledač odluči da li klik šalje formu. Narudžba bi tako nastala
                  bez koraka 3, pa kupac nikad ne bi vidio promo kod ni poene. */}
              {step < 3 ? (
                <Button
                  key="nastavi"
                  type="button"
                  onClick={() => setStep((s) => s + 1)}
                  disabled={(step === 1 && !step1Valid) || (step === 2 && !step2Valid)}
                >
                  Nastavi <ArrowRight className="h-4 w-4" />
                </Button>
              ) : (
                <Button key="naruci" type="submit" disabled={!canOrder || activeMutation.isPending || (!!promoKod && promo.isFetching)}>
                  {activeMutation.isPending ? "Šaljem…" : "Naruči"}
                </Button>
              )}
            </div>
          </div>
        </form>

        <aside className="hidden lg:sticky lg:top-28 lg:block">
          <OrderSummaryPanel
            items={items}
            subtotal={subtotal}
            deliveryFee={deliveryFee}
            total={total}
            zoneName={selectedZone?.name}
            popust={popust}
            promoKod={promo.data?.kod}
            poeniPopust={poeniPopust}
            iskoristeniPoeni={poenaZaTrosenje}
          />
        </aside>
      </div>

      <p className="mt-3 text-center text-xs text-muted">Plaćanje pouzećem pri dostavi.</p>
    </div>
  );
}
