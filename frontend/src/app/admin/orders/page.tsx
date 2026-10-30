"use client";

import { Suspense, useEffect, useRef, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Columns3, List, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/components/ui/toast";
import { OrderCard } from "@/components/admin/order-card";
import { NarudzbeTabla } from "@/components/admin/narudzbe-tabla";
import { StanjeZaliha } from "@/components/admin/stanje-zaliha";
import { ZaPotvrdu } from "@/components/admin/za-potvrdu";
import { RobaZaSedmicu } from "@/components/admin/roba-za-sedmicu";
import { IzborSedmice, useIzabranaSedmica } from "@/components/admin/izbor-sedmice";
import { Input } from "@/components/ui/field";
import { ApiRequestError } from "@/lib/api/client";
import { adminApi } from "@/lib/api/endpoints";
import { STATUS_META, narudzbeRijec } from "@/lib/orders";
import { ORDER_STATUSES, type Order, type OrderStatus } from "@/types/domain";
import { cn } from "@/lib/utils";

export default function AdminOrdersPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <AdminOrders />
    </Suspense>
  );
}

function AdminOrders() {
  const params = useSearchParams();
  const izbor = useIzabranaSedmica();
  // Filter dolazi i iz linka sa početnog ekrana (?status=Pending).
  const [filter, setFilter] = useState<OrderStatus | null>(
    () => {
      const s = params.get("status");
      return s && s in STATUS_META ? (s as OrderStatus) : null;
    },
  );
  // Pretraga dolazi i iz linka (npr. sa ekrana kupca: ?pretraga=EP-2610-0063).
  const [unos, setUnos] = useState(params.get("pretraga") ?? "");
  const [pretraga, setPretraga] = useState(unos.trim());
  const [poSedmici, setPoSedmici] = useState(params.has("datum"));
  useEffect(() => {
    const t = setTimeout(() => setPretraga(unos.trim()), 300);
    return () => clearTimeout(t);
  }, [unos]);
  const sedmica = poSedmici ? { godina: izbor.godina, sedmica: izbor.sedmica } : {};
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const qc = useQueryClient();
  const { show } = useToast();

  // Tabla je podrazumijevani prikaz; lista ostaje za filtriranje po statusu (i dolazi sa ?status=).
  const router = useRouter();
  const pathname = usePathname();
  const [prikaz, setPrikazStanje] = useState<"tabla" | "lista">(() => {
    const p = params.get("prikaz");
    if (p === "tabla" || p === "lista") return p;
    return params.get("status") ? "lista" : "tabla";
  });
  function setPrikaz(p: "tabla" | "lista") {
    setPrikazStanje(p);
    const next = new URLSearchParams(params.toString());
    next.set("prikaz", p);
    router.replace(`${pathname}?${next}`, { scroll: false });
  }
  const status = prikaz === "tabla" ? null : filter;

  const kljuc = ["admin", "orders", { status, pretraga, ...sedmica }] as const;
  const orders = useQuery({
    queryKey: kljuc,
    queryFn: () => adminApi.orders({ status: status ?? undefined, pretraga: pretraga || undefined, ...sedmica }),
    // Nove narudžbe stižu same; pauzira se kad je tab u pozadini.
    refetchInterval: 30_000,
  });

  // Obavještenje za narudžbu koja je stigla dok je ekran otvoren (ne pri prvom učitavanju).
  const vidjene = useRef<Set<string> | null>(null);
  useEffect(() => {
    if (!orders.data) return;
    const naCekanju = orders.data.filter((o) => o.status === "Pending");
    if (vidjene.current) {
      const nove = naCekanju.filter((o) => !vidjene.current!.has(o.id));
      if (nove.length === 1) show(`Nova narudžba ${nove[0].code} — ${nove[0].customerName}`, "info");
      else if (nove.length > 1) show(`${nove.length} nove narudžbe čekaju potvrdu`, "info");
    }
    vidjene.current = new Set([...(vidjene.current ?? []), ...naCekanju.map((o) => o.id)]);
  }, [orders.data, show]);

  /** Optimistično pomjeri narudžbu u svim keširanim listama narudžbi. */
  function pomjeri(id: string, status: OrderStatus) {
    qc.setQueriesData<Order[]>({ queryKey: ["admin", "orders"] }, (l) =>
      Array.isArray(l) ? l.map((o) => (o.id === id ? { ...o, status } : o)) : l,
    );
  }
  function osvjezi() {
    qc.invalidateQueries({ queryKey: ["admin", "orders"] });
    qc.invalidateQueries({ queryKey: ["admin", "dashboard"] });
    qc.invalidateQueries({ queryKey: ["admin", "zadaci"] });
  }

  const vise = useMutation({
    mutationFn: async ({ lista, status }: { lista: Order[]; status: OrderStatus }) => {
      lista.forEach((o) => pomjeri(o.id, status));
      const r = await Promise.allSettled(lista.map((o) => adminApi.updateOrderStatus(o.id, status)));
      return { uspjelo: r.filter((x) => x.status === "fulfilled").length, ukupno: lista.length, status };
    },
    onSuccess: ({ uspjelo, ukupno, status }) => {
      osvjezi();
      show(
        uspjelo === ukupno
          ? `${STATUS_META[status].label}: ${ukupno} ${narudzbeRijec(ukupno)}`
          : `Promijenjeno ${uspjelo} od ${ukupno} — ostale nisu mogle preći u „${STATUS_META[status].label}"`,
        uspjelo === ukupno ? "success" : "error",
      );
    },
  });

  const mutation = useMutation({
    mutationFn: ({ id, status }: { id: string; status: OrderStatus; code?: string }) =>
      adminApi.updateOrderStatus(id, status),
    onMutate: ({ id, status }) => pomjeri(id, status),
    onSuccess: (_data, { status, code }) => {
      osvjezi();
      show(`${code ? `${code} → ` : "Status promijenjen: "}${STATUS_META[status].label}`);
    },
    onError: (err) => {
      // Vrati karticu gdje je bila — server je izvor istine.
      osvjezi();
      show(err instanceof ApiRequestError ? err.message : "Status nije promijenjen", "error");
    },
  });

  function toggle(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Narudžbe</h1>
          <p className="text-muted">
            {prikaz === "tabla"
              ? "Prevuci karticu u sljedeću kolonu ili klikni strelicu; klik na karticu otvara detalje."
              : "Klikni narudžbu da je proširiš i promijeniš status."}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex rounded-full bg-white p-0.5 ring-1 ring-black/10" role="group" aria-label="Prikaz">
            {([
              ["tabla", "Tabla", Columns3],
              ["lista", "Lista", List],
            ] as const).map(([k, naziv, Ikona]) => (
              <button
                key={k}
                type="button"
                aria-pressed={prikaz === k}
                onClick={() => setPrikaz(k)}
                className={cn(
                  "flex items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium transition",
                  prikaz === k ? "bg-ink text-white" : "text-muted hover:text-ink",
                )}
              >
                <Ikona className="h-4 w-4" /> {naziv}
              </button>
            ))}
          </div>
          <Link href="/admin/orders/new">
            <Button size="sm">
              <Plus className="h-4 w-4" /> Nova narudžba
            </Button>
          </Link>
        </div>
      </div>

      <details open className="group rounded-card border border-line bg-surface">
        <summary className="cursor-pointer list-none px-5 py-3 font-display text-lg font-bold text-ink">
          Nabavka za sedmicu {izbor.sedmica}. — koliko robe treba
          <span className="ml-2 text-sm font-normal text-muted group-open:hidden">(otvori)</span>
        </summary>
        <div className="space-y-3 border-t border-line px-5 py-4">
          <IzborSedmice s={izbor} />
          <RobaZaSedmicu godina={izbor.godina} sedmica={izbor.sedmica} />
        </div>
      </details>

      <div className="flex flex-wrap items-center gap-3">
        <div className="w-full max-w-xs">
          <Input
            type="search"
            aria-label="Pretraga narudžbi"
            value={unos}
            onChange={(e) => setUnos(e.target.value)}
            placeholder="Šifra, kupac, telefon ili adresa"
          />
        </div>
        <label className="flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={poSedmici} onChange={(e) => setPoSedmici(e.target.checked)} className="h-4 w-4 accent-brand-600" />
          Samo sedmica isporuke
        </label>
        {poSedmici && <IzborSedmice s={izbor} />}
        <Link href="/admin/pakovanje" className="text-sm font-medium text-brand-700 hover:underline">
          Pakovanje sedmice →
        </Link>
      </div>

      <ZaPotvrdu />

      <StanjeZaliha />

      {prikaz === "lista" && (
      <div className="no-scrollbar -mx-1 flex gap-2 overflow-x-auto px-1">
        <FilterChip label="Sve" active={filter === null} onClick={() => setFilter(null)} />
        {ORDER_STATUSES.map((st) => (
          <FilterChip
            key={st}
            label={STATUS_META[st].label}
            active={filter === st}
            onClick={() => setFilter(st)}
          />
        ))}
      </div>
      )}

      {orders.isLoading ? (
        <LoadingBlock />
      ) : prikaz === "tabla" ? (
        <NarudzbeTabla
          narudzbe={orders.data ?? []}
          pending={mutation.isPending || vise.isPending}
          onPromijeni={(o, s) => mutation.mutate({ id: o.id, status: s, code: o.code })}
          onPromijeniVise={(lista, s) => vise.mutate({ lista, status: s })}
        />
      ) : orders.isLoading ? (
        <LoadingBlock />
      ) : orders.data && orders.data.length > 0 ? (
        <div className="space-y-3">
          {orders.data.map((o) => (
            <OrderCard
              key={o.id}
              order={o}
              expanded={expanded.has(o.id)}
              pending={mutation.isPending}
              onToggle={() => toggle(o.id)}
              onAdvance={(status) => mutation.mutate({ id: o.id, status })}
            />
          ))}
        </div>
      ) : (
        <EmptyState title="Nema narudžbi" description="Za odabrani filter nema rezultata." />
      )}
    </div>
  );
}

function FilterChip({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      onClick={onClick}
      className={cn(
        "whitespace-nowrap rounded-full px-3.5 py-1.5 text-sm font-medium transition",
        active ? "bg-ink text-white" : "bg-white text-ink ring-1 ring-black/10 hover:bg-sand",
      )}
    >
      {label}
    </button>
  );
}
