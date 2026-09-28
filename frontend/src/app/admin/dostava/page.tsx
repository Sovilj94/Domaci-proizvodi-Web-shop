"use client";

import { Suspense, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowRightLeft, ChevronDown, Inbox, Plus, Search, Sparkles, Truck, X } from "lucide-react";
import { DispatchTabs, inicijali, type DispatchTab } from "@/components/admin/dispatch-tabs";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { adminApi } from "@/lib/api/endpoints";
import { narudzbeRijec } from "@/lib/orders";
import type { Driver } from "@/lib/api/contracts";
import type { DeliveryRoute, Order, RouteStop } from "@/types/domain";
import { cn } from "@/lib/utils";

const NEDODIJELJENO = "nedodijeljeno";

/** Statusi u kojima je stanica završena — prigušuje se i gubi dugmad. */
const ZATVORENI = new Set(["Delivered", "FailedDelivery", "Cancelled"]);

// Pre-existing gap, not introduced by the checkout-nalog gate: useSearchParams() here also
// opts the page out of static generation without a Suspense boundary below it.
export default function AdminDostavaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <AdminDostavaContent />
    </Suspense>
  );
}

function AdminDostavaContent() {
  const qc = useQueryClient();
  const { show } = useToast();
  const router = useRouter();
  const params = useSearchParams();

  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [pretraga, setPretraga] = useState("");
  const [prebacujem, setPrebacujem] = useState<string | null>(null);

  // Aktivni tab živi u URL-u da osvježavanje stranice ne izgubi izbor vozača.
  const activeId = params.get("vozac") ?? NEDODIJELJENO;
  const selectTab = (id: string) => {
    const next = new URLSearchParams(params.toString());
    if (id === NEDODIJELJENO) next.delete("vozac");
    else next.set("vozac", id);
    router.replace(next.toString() ? `?${next}` : "?", { scroll: false });
  };

  const driversKey = ["admin", "drivers"] as const;
  const routesKey = ["admin", "routes", date] as const;
  const unassignedKey = ["admin", "orders", "unassigned", date] as const;

  const drivers = useQuery({ queryKey: driversKey, queryFn: () => adminApi.drivers() });
  const routes = useQuery({ queryKey: routesKey, queryFn: () => adminApi.routes(date) });
  const nedodijeljene = useQuery({
    queryKey: unassignedKey,
    queryFn: () => adminApi.orders({ unassigned: true, date }),
  });

  /** Stanice po vozaču, spojene iz svih njegovih ruta tog dana. */
  const poVozacu = useMemo(() => {
    const mapa = new Map<string, RouteStop[]>();
    for (const r of routes.data ?? []) {
      if (!r.driverId) continue;
      const dosad = mapa.get(r.driverId) ?? [];
      mapa.set(r.driverId, [...dosad, ...r.stops]);
    }
    // Otvorene prvo (po redoslijedu vožnje), zatvorene na kraj — isto pravilo koje
    // aplikacija vozača koristi na svom ekranu rute.
    for (const [id, stanice] of mapa) {
      mapa.set(
        id,
        [...stanice].sort((a, b) => {
          const az = ZATVORENI.has(a.status ?? "") ? 1 : 0;
          const bz = ZATVORENI.has(b.status ?? "") ? 1 : 0;
          return az !== bz ? az - bz : a.sequence - b.sequence;
        }),
      );
    }
    return mapa;
  }, [routes.data]);

  /**
   * Tabovi: „Nedodijeljeno" pa aktivni vozači. Neaktivan vozač dobija tab SAMO ako mu je
   * nešto već dodijeljeno tog dana — inače bi njegove narudžbe nestale sa ekrana.
   */
  const tabs: DispatchTab[] = useMemo(() => {
    const vozaci = (drivers.data ?? []).filter(
      (d) => d.aktivan || (poVozacu.get(d.id)?.length ?? 0) > 0,
    );
    return [
      {
        id: NEDODIJELJENO,
        label: "Nedodijeljeno",
        count: nedodijeljene.data?.length ?? 0,
      },
      ...vozaci.map((d) => ({
        id: d.id,
        label: d.name,
        initials: inicijali(d.name),
        count: poVozacu.get(d.id)?.length ?? 0,
        muted: !d.aktivan,
      })),
    ];
  }, [drivers.data, poVozacu, nedodijeljene.data]);

  const aktivniVozac: Driver | undefined = (drivers.data ?? []).find((d) => d.id === activeId);

  function osvjezi() {
    qc.invalidateQueries({ queryKey: routesKey });
    qc.invalidateQueries({ queryKey: unassignedKey });
  }

  /**
   * Optimistic update: narudžba se odmah premjesti iz nedodijeljenih u vozačeve stanice
   * (i brojevi u tabovima se pomjere), pa se tek onda čeka server. `onError` vraća obje
   * liste na snimak od prije izmjene — bez toga bi ekran ostao da laže poslije neuspjeha.
   */
  const dodijeli = useMutation({
    mutationFn: ({ orderId, driverId }: { orderId: string; driverId: string }) =>
      adminApi.assignOrderDriver(orderId, { driverId, date }),
    onMutate: async ({ orderId, driverId }) => {
      await Promise.all([
        qc.cancelQueries({ queryKey: routesKey }),
        qc.cancelQueries({ queryKey: unassignedKey }),
      ]);
      const prijeRuta = qc.getQueryData<DeliveryRoute[]>(routesKey);
      const prijeNedodijeljenih = qc.getQueryData<Order[]>(unassignedKey);

      const narudzba = prijeNedodijeljenih?.find((o) => o.id === orderId);
      const izRute = prijeRuta
        ?.flatMap((r) => r.stops)
        .find((s) => s.orderId === orderId);

      qc.setQueryData<Order[]>(unassignedKey, (stare) =>
        (stare ?? []).filter((o) => o.id !== orderId),
      );

      qc.setQueryData<DeliveryRoute[]>(routesKey, (stare) => {
        if (!stare) return stare;
        // Skini je sa stare rute (prebacivanje), pa je zalijepi na prvu rutu ciljanog
        // vozača. Ako je vozač nema nijednu, optimistički prikaz preskače korak i čeka
        // server — praviti lažnu rutu sa izmišljenim id-em ne bi imalo šta da prikaže.
        const bez = stare.map((r) => ({
          ...r,
          stops: r.stops.filter((s) => s.orderId !== orderId),
        }));
        const ciljna = bez.find((r) => r.driverId === driverId);
        if (!ciljna) return bez;

        const stanica: RouteStop = izRute ?? {
          orderId,
          orderCode: narudzba?.code ?? "",
          customerName: narudzba?.customerName ?? "",
          address: narudzba?.deliveryAddress ?? "",
          zoneName: narudzba?.deliveryZoneName ?? "",
          sequence: ciljna.stops.length + 1,
          status: narudzba?.status,
          totalAmount: narudzba?.totalAmount,
        };

        return bez.map((r) =>
          r.id === ciljna.id ? { ...r, stops: [...r.stops, stanica] } : r,
        );
      });

      return { prijeRuta, prijeNedodijeljenih };
    },
    onError: (err, _vars, ctx) => {
      if (ctx?.prijeRuta) qc.setQueryData(routesKey, ctx.prijeRuta);
      if (ctx?.prijeNedodijeljenih) qc.setQueryData(unassignedKey, ctx.prijeNedodijeljenih);
      show(poruka(err, "Dodjela nije uspjela."), "error");
    },
    onSettled: osvjezi,
  });

  const skini = useMutation({
    mutationFn: (orderId: string) => adminApi.removeOrderDriver(orderId, date),
    onMutate: async (orderId) => {
      await Promise.all([
        qc.cancelQueries({ queryKey: routesKey }),
        qc.cancelQueries({ queryKey: unassignedKey }),
      ]);
      const prijeRuta = qc.getQueryData<DeliveryRoute[]>(routesKey);
      const prijeNedodijeljenih = qc.getQueryData<Order[]>(unassignedKey);

      qc.setQueryData<DeliveryRoute[]>(routesKey, (stare) =>
        (stare ?? []).map((r) => ({
          ...r,
          stops: r.stops.filter((s) => s.orderId !== orderId),
        })),
      );
      // Lijeva strana se prazni odmah; desna lista nedodijeljenih čeka server, jer
      // RouteStop nema sve što Order kartica prikazuje (termin, iznos stavki).
      return { prijeRuta, prijeNedodijeljenih };
    },
    onError: (err, _orderId, ctx) => {
      if (ctx?.prijeRuta) qc.setQueryData(routesKey, ctx.prijeRuta);
      if (ctx?.prijeNedodijeljenih) qc.setQueryData(unassignedKey, ctx.prijeNedodijeljenih);
      show(poruka(err, "Uklanjanje nije uspjelo."), "error");
    },
    onSettled: osvjezi,
  });

  /**
   * Grupna dodjela: cijelo naselje jednom vozaču, jedan zahtjev. Namjerno bez optimističkog
   * prikaza koji `dodijeli` ima — server je taj koji zna staje li grupa u kapacitet, a
   * pomjeriti petnaest kartica pa ih vraćati na `driver_full` je gore nego sačekati odgovor.
   */
  const dodijeliGrupu = useMutation({
    mutationFn: ({ orderIds, driverId }: { orderIds: string[]; driverId: string }) =>
      adminApi.assignOrdersBulk({ orderIds, driverId, date }),
    onSuccess: (_data, { orderIds, driverId }) => {
      const vozac = (drivers.data ?? []).find((d) => d.id === driverId);
      show(
        `${orderIds.length} ${narudzbeRijec(orderIds.length)} → ${vozac?.name ?? "vozač"}`,
      );
    },
    onError: (err) => show(poruka(err, "Grupna dodjela nije uspjela."), "error"),
    onSettled: osvjezi,
  });

  const ucitava = drivers.isLoading || routes.isLoading || nedodijeljene.isLoading;

  const stanice = aktivniVozac ? (poVozacu.get(aktivniVozac.id) ?? []) : [];
  const zauzeto = stanice.length;
  const slobodno = Math.max(0, (aktivniVozac?.maxStanica ?? 0) - zauzeto);

  const dodijeljeniIds = useMemo(
    () => new Set((routes.data ?? []).flatMap((r) => r.stops.map((s) => s.orderId))),
    [routes.data],
  );

  const zaDodavanje = useMemo(() => {
    const q = pretraga.trim().toLowerCase();
    return (nedodijeljene.data ?? [])
      .filter((o) => !dodijeljeniIds.has(o.id))
      .filter(
        (o) =>
          !q ||
          o.code.toLowerCase().includes(q) ||
          o.customerName.toLowerCase().includes(q) ||
          o.deliveryAddress.toLowerCase().includes(q),
      );
  }, [nedodijeljene.data, dodijeljeniIds, pretraga]);

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold text-ink">Dostava</h1>
          <p className="mt-1 text-sm text-muted">
            Izaberi vozača i rasporedi mu narudžbe za dan.
          </p>
        </div>
        <div className="flex items-end gap-3">
          <label className="flex flex-col gap-1.5">
            <span className="text-sm font-medium text-ink">Datum dostave</span>
            <Input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              className="w-44"
            />
          </label>
          {/* Optimizacija, geokodiranje i ručno postavljanje lokacije žive na starom
              ekranu ruta. Van obima ovog ekrana, ali ne smiju ostati bez ulaza. */}
          <Link href="/admin/routes" className={buttonVariants({ variant: "secondary" })}>
            <Sparkles className="h-4 w-4" />
            Optimizacija ruta
          </Link>
        </div>
      </div>

      <DispatchTabs tabs={tabs} activeId={activeId} onSelect={selectTab} />

      {ucitava ? (
        <LoadingBlock />
      ) : activeId === NEDODIJELJENO ? (
        <NedodijeljeneTab
          narudzbe={zaDodavanje}
          vozaci={(drivers.data ?? []).filter((d) => d.aktivan)}
          pretraga={pretraga}
          onPretraga={setPretraga}
          onDodijeli={(orderId, driverId) => dodijeli.mutate({ orderId, driverId })}
          onDodijeliGrupu={(orderIds, driverId) =>
            dodijeliGrupu.mutate({ orderIds, driverId })
          }
          uToku={dodijeli.isPending || dodijeliGrupu.isPending}
        />
      ) : !aktivniVozac ? (
        <EmptyState
          title="Vozač nije pronađen"
          description="Možda je obrisan. Izaberi drugog iz trake iznad."
        />
      ) : (
        <>
          <VozacHeader vozac={aktivniVozac} zauzeto={zauzeto} slobodno={slobodno} />

          {/* items-start: bez toga grid rasteže lijevu karticu na visinu bočne liste, pa
              prazno stanje završi centrirano stotinama piksela ispod pregiba. */}
          <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_360px]">
            <StaniceLista
              stanice={stanice}
              onSkini={(orderId) => skini.mutate(orderId)}
              onPrebaci={setPrebacujem}
              prebacujem={prebacujem}
              vozaci={(drivers.data ?? []).filter(
                (d) => d.aktivan && d.id !== aktivniVozac.id,
              )}
              onIzabranVozac={(orderId, driverId) => {
                setPrebacujem(null);
                dodijeli.mutate({ orderId, driverId });
              }}
            />

            {zaDodavanje.length > 0 || pretraga ? (
              <DodajKartica
                vozac={aktivniVozac}
                narudzbe={zaDodavanje}
                pretraga={pretraga}
                onPretraga={setPretraga}
                onDodaj={(orderId) =>
                  dodijeli.mutate({ orderId, driverId: aktivniVozac.id })
                }
                uToku={dodijeli.isPending}
              />
            ) : null}
          </div>
        </>
      )}
    </div>
  );
}

/* ---------- Header vozača ---------- */

function VozacHeader({
  vozac,
  zauzeto,
  slobodno,
}: {
  vozac: Driver;
  zauzeto: number;
  slobodno: number;
}) {
  const postotak = Math.min(100, Math.round((zauzeto / Math.max(1, vozac.maxStanica)) * 100));
  const pun = slobodno === 0;

  return (
    <div className="flex flex-wrap items-center gap-4 rounded-card border border-line bg-white p-5 shadow-card">
      <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-brand-50 text-sm font-bold text-brand-700">
        {inicijali(vozac.name)}
      </span>

      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <h2 className="font-display text-lg font-bold text-ink">{vozac.name}</h2>
          <span
            className={cn(
              "rounded-full px-2.5 py-0.5 text-xs font-semibold",
              vozac.aktivan ? "bg-brand-50 text-brand-700" : "bg-sand text-muted",
            )}
          >
            {vozac.aktivan ? "Aktivan" : "Neaktivan"}
          </span>
        </div>
        {vozac.phone && <p className="text-sm text-muted">{vozac.phone}</p>}
      </div>

      <div className="ml-auto w-full max-w-xs">
        <div className="flex items-baseline justify-between text-sm">
          <span className="font-semibold tabular-nums text-ink">
            {zauzeto} / {vozac.maxStanica}
          </span>
          <span className={cn("text-xs", pun ? "font-semibold text-red-600" : "text-muted")}>
            {pun ? "nema slobodnih mjesta" : `${slobodno} slobodnih mjesta`}
          </span>
        </div>
        <div className="mt-1.5 h-2 overflow-hidden rounded-full bg-sand">
          <div
            className={cn("h-full rounded-full", pun ? "bg-red-500" : "bg-brand-500")}
            style={{ width: `${postotak}%` }}
          />
        </div>
      </div>
    </div>
  );
}

/* ---------- Lijeva kolona: dodijeljene stanice ---------- */

function StaniceLista({
  stanice,
  onSkini,
  onPrebaci,
  prebacujem,
  vozaci,
  onIzabranVozac,
}: {
  stanice: RouteStop[];
  onSkini: (orderId: string) => void;
  onPrebaci: (orderId: string | null) => void;
  prebacujem: string | null;
  vozaci: Driver[];
  onIzabranVozac: (orderId: string, driverId: string) => void;
}) {
  if (stanice.length === 0) {
    return (
      <EmptyState
        icon={<Truck className="h-7 w-7" />}
        title="Nema dodijeljenih narudžbi"
        description="Dodaj mu narudžbu iz kartice desno."
      />
    );
  }

  return (
    <div className="divide-y divide-line overflow-hidden rounded-card border border-line bg-white shadow-card">
      {stanice.map((s) => {
        const zatvorena = ZATVORENI.has(s.status ?? "");
        const biram = prebacujem === s.orderId;

        return (
          <div key={s.orderId} className={cn("p-4", zatvorena && "opacity-50")}>
            <div className="flex items-start gap-3">
              <span
                className={cn(
                  "mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-full text-xs font-bold",
                  zatvorena ? "bg-sand text-muted" : "bg-ink text-white",
                )}
              >
                {zatvorena ? "✓" : s.sequence}
              </span>

              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-bold tabular-nums text-ink">{s.orderCode}</span>
                  <span className="text-sm text-ink">{s.customerName}</span>
                  {zatvorena && s.zatvorenaU && (
                    <span className="text-xs text-muted">
                      isporučeno u {vrijeme(s.zatvorenaU)}
                    </span>
                  )}
                </div>
                <p className="mt-0.5 truncate text-sm text-muted">
                  {s.address}
                  {s.zoneName && ` · ${s.zoneName}`}
                </p>
              </div>

              {!zatvorena && (
                <div className="flex shrink-0 items-center gap-2">
                  <Button
                    variant="secondary"
                    size="sm"
                    onClick={() => onPrebaci(biram ? null : s.orderId)}
                  >
                    <ArrowRightLeft className="h-4 w-4" />
                    Prebaci
                  </Button>
                  <button
                    type="button"
                    aria-label={`Ukloni ${s.orderCode} sa rute`}
                    onClick={() => onSkini(s.orderId)}
                    className="grid h-9 w-9 place-items-center rounded-xl border border-line text-muted transition hover:bg-sand hover:text-ink"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>
              )}
            </div>

            {biram && (
              <div className="mt-3 flex flex-wrap gap-2 rounded-xl bg-sand p-3">
                {vozaci.length === 0 ? (
                  <p className="text-sm text-muted">Nema drugog aktivnog vozača.</p>
                ) : (
                  vozaci.map((d) => (
                    <Button
                      key={d.id}
                      variant="secondary"
                      size="sm"
                      onClick={() => onIzabranVozac(s.orderId, d.id)}
                    >
                      {d.name}
                    </Button>
                  ))
                )}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

/* ---------- Desna kartica: dodavanje ---------- */

function DodajKartica({
  vozac,
  narudzbe,
  pretraga,
  onPretraga,
  onDodaj,
  uToku,
}: {
  vozac: Driver;
  narudzbe: Order[];
  pretraga: string;
  onPretraga: (v: string) => void;
  onDodaj: (orderId: string) => void;
  uToku: boolean;
}) {
  return (
    <aside className="h-fit rounded-card border border-line bg-white p-4 shadow-card">
      <h3 className="font-display text-base font-bold text-ink">
        Dodaj narudžbu — {vozac.name}
      </h3>

      <div className="relative mt-3">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
        <Input
          value={pretraga}
          onChange={(e) => onPretraga(e.target.value)}
          placeholder="Traži po šifri, kupcu ili adresi…"
          className="pl-9"
        />
      </div>

      {/* Ograničena visina sa vlastitim skrolom: nedodijeljenih zna biti pedesetak, a bez
          ovoga bočna kartica razvuče cijelu stranicu i lijeva kolona ostane u praznini. */}
      <div className="mt-3 max-h-[26rem] space-y-2 overflow-y-auto pr-1">
        {narudzbe.length === 0 ? (
          <p className="py-6 text-center text-sm text-muted">Nema rezultata.</p>
        ) : (
          narudzbe.map((o) => (
            <div
              key={o.id}
              className="flex items-start gap-3 rounded-xl border border-line p-3"
            >
              <div className="min-w-0 flex-1">
                <p className="text-sm font-bold tabular-nums text-ink">{o.code}</p>
                <p className="truncate text-sm text-muted">{o.deliveryAddress}</p>
              </div>
              <Button size="sm" disabled={uToku} onClick={() => onDodaj(o.id)}>
                <Plus className="h-4 w-4" />
                Dodaj
              </Button>
            </div>
          ))
        )}
      </div>
    </aside>
  );
}

/* ---------- Tab „Nedodijeljeno" ---------- */

/**
 * Grupiše narudžbe po naselju (zoni dostave). Redoslijed je abecedni po nazivu naselja.
 *
 * Ranije je bio po veličini opadajuće, da dispečer prvo vidi najveće naselje. To se u radu
 * pokazalo kao smetnja: svaka dodjela smanji grupu, grupa padne niže, i lista se premetne
 * pod rukom dok se klika. Abecedni redoslijed se ne mijenja dok se radi, pa naselje ostaje
 * tamo gdje ga je dispečer zadnji put vidio.
 *
 * Poređenje ide kroz `localeCompare` sa srpskim lokalitetom — obično `<` bi Č, Ć, Š, Ž i Đ
 * poslalo iza Z, po redoslijedu Unicode tačaka, umjesto na njihovo mjesto u azbučnom nizu.
 *
 * Narudžbe bez zone padaju u „Bez naselja" i idu na kraj, isto kao `NazivBezZone` na serveru.
 *
 * NAPOMENA: ovo je redoslijed liste za dispečera, ne redoslijed vožnje. Stanice unutar
 * vozačevog taba ostaju poredane po `sequence`, koji dolazi iz OSRM optimizacije — abecedno
 * tamo bi zamijenilo najkraću rutu nasumičnom.
 */
const BEZ_NASELJA = "Bez naselja";

function grupisiPoNaselju(narudzbe: Order[]): { naselje: string; narudzbe: Order[] }[] {
  const mapa = new Map<string, Order[]>();
  for (const o of narudzbe) {
    const kljuc = o.deliveryZoneName?.trim() || BEZ_NASELJA;
    mapa.set(kljuc, [...(mapa.get(kljuc) ?? []), o]);
  }
  return [...mapa.entries()]
    .map(([naselje, narudzbe]) => ({ naselje, narudzbe }))
    .sort((a, b) => {
      if (a.naselje === BEZ_NASELJA) return 1;
      if (b.naselje === BEZ_NASELJA) return -1;
      return a.naselje.localeCompare(b.naselje, "sr");
    });
}

function NedodijeljeneTab({
  narudzbe,
  vozaci,
  pretraga,
  onPretraga,
  onDodijeli,
  onDodijeliGrupu,
  uToku,
}: {
  narudzbe: Order[];
  vozaci: Driver[];
  pretraga: string;
  onPretraga: (v: string) => void;
  onDodijeli: (orderId: string, driverId: string) => void;
  onDodijeliGrupu: (orderIds: string[], driverId: string) => void;
  uToku: boolean;
}) {
  const grupe = useMemo(() => grupisiPoNaselju(narudzbe), [narudzbe]);

  if (narudzbe.length === 0 && !pretraga) {
    return (
      <EmptyState
        icon={<Inbox className="h-7 w-7" />}
        title="Sve je raspoređeno"
        description="Nema narudžbi koje čekaju vozača za izabrani dan."
      />
    );
  }

  return (
    <div className="space-y-4">
      <div className="relative max-w-sm">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
        <Input
          value={pretraga}
          onChange={(e) => onPretraga(e.target.value)}
          placeholder="Traži po šifri, kupcu ili adresi…"
          className="pl-9"
        />
      </div>

      {narudzbe.length === 0 ? (
        <p className="py-6 text-center text-sm text-muted">
          Nema rezultata za „{pretraga}&rdquo;.
        </p>
      ) : (
        <div className="space-y-4">
          {grupe.map((g) => (
            <NaseljeGrupa
              key={g.naselje}
              naselje={g.naselje}
              narudzbe={g.narudzbe}
              vozaci={vozaci}
              onDodijeli={onDodijeli}
              onDodijeliGrupu={onDodijeliGrupu}
              uToku={uToku}
            />
          ))}
        </div>
      )}
    </div>
  );
}

/* ---------- Jedno naselje: zaglavlje sa grupnom dodjelom + spisak narudžbi ---------- */

function NaseljeGrupa({
  naselje,
  narudzbe,
  vozaci,
  onDodijeli,
  onDodijeliGrupu,
  uToku,
}: {
  naselje: string;
  narudzbe: Order[];
  vozaci: Driver[];
  onDodijeli: (orderId: string, driverId: string) => void;
  onDodijeliGrupu: (orderIds: string[], driverId: string) => void;
  uToku: boolean;
}) {
  const [otvorena, setOtvorena] = useState(true);
  const idevi = narudzbe.map((o) => o.id);

  return (
    <section className="overflow-hidden rounded-card border border-line bg-white shadow-card">
      <header className="flex flex-wrap items-center gap-3 border-b border-line bg-sand/40 p-4">
        <button
          type="button"
          onClick={() => setOtvorena((v) => !v)}
          aria-expanded={otvorena}
          className="flex min-w-0 items-center gap-2 text-left"
        >
          <ChevronDown
            className={cn(
              "h-4 w-4 shrink-0 text-muted transition-transform",
              !otvorena && "-rotate-90",
            )}
          />
          <h3 className="font-display text-base font-bold text-ink">{naselje}</h3>
          <span className="rounded-full bg-ink px-2 py-0.5 text-xs font-bold tabular-nums text-white">
            {narudzbe.length}
          </span>
        </button>

        <div className="ml-auto flex flex-wrap items-center gap-2">
          {vozaci.length === 0 ? (
            <span className="text-sm text-muted">Nema aktivnog vozača.</span>
          ) : (
            <>
              <span className="text-sm text-muted">Cijelo naselje:</span>
              {vozaci.map((d) => (
                <Button
                  key={d.id}
                  size="sm"
                  disabled={uToku}
                  onClick={() => onDodijeliGrupu(idevi, d.id)}
                  title={`Dodijeli svih ${narudzbe.length} ${narudzbeRijec(narudzbe.length)} vozaču ${d.name}`}
                >
                  <Truck className="h-4 w-4" />
                  {d.name}
                </Button>
              ))}
            </>
          )}
        </div>
      </header>

      {otvorena && (
        <div className="divide-y divide-line">
          {narudzbe.map((o) => (
            <div key={o.id} className="flex flex-wrap items-start gap-3 p-4">
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-sm font-bold tabular-nums text-ink">{o.code}</span>
                  <span className="text-sm text-ink">{o.customerName}</span>
                </div>
                <p className="mt-0.5 truncate text-sm text-muted">{o.deliveryAddress}</p>
              </div>

              <div className="flex flex-wrap items-center gap-2">
                {vozaci.map((d) => (
                  <Button
                    key={d.id}
                    variant="secondary"
                    size="sm"
                    disabled={uToku}
                    onClick={() => onDodijeli(o.id, d.id)}
                  >
                    <Plus className="h-4 w-4" />
                    {d.name}
                  </Button>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

/* ---------- Sitnice ---------- */

function vrijeme(iso: string): string {
  return new Date(iso).toLocaleTimeString("sr-Latn-BA", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

/** Poruka iz ApiRequestError ako je ima; inače rezerva. */
function poruka(err: unknown, rezerva: string): string {
  const m = (err as { message?: string } | null)?.message;
  return m && m.trim() ? m : rezerva;
}
