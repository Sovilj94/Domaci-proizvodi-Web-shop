"use client";

import Link from "next/link";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowRight,
  Banknote,
  CheckCircle2,
  Clock,
  Info,
  ShoppingBag,
  Sparkles,
  Truck,
} from "lucide-react";
import { StatCard } from "@/components/admin/stat-card";
import { StatusBadge } from "@/components/ui/status-badge";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { adminApi, adminKorpiceApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import type { AdminZadatak } from "@/lib/api/contracts";
import { formatPrice, formatDateTime, cn } from "@/lib/utils";

const NIVO = {
  hitno: { ikona: AlertTriangle, okvir: "border-l-red-500", boja: "bg-red-50 text-red-600", natpis: "Hitno" },
  upozorenje: { ikona: AlertTriangle, okvir: "border-l-honey-500", boja: "bg-honey-100 text-honey-700", natpis: "Provjeri" },
  info: { ikona: Info, okvir: "border-l-brand-500", boja: "bg-brand-50 text-brand-700", natpis: "Za danas" },
} as const;

export default function AdminDashboardPage() {
  const today = new Date().toISOString();
  const zadaci = useQuery({
    queryKey: ["admin", "zadaci"],
    queryFn: () => adminApi.zadaci(),
    refetchInterval: 30_000,
  });
  const stats = useQuery({
    queryKey: ["admin", "dashboard"],
    queryFn: () => adminApi.dashboard(),
  });
  const orders = useQuery({
    queryKey: ["admin", "orders", "today"],
    queryFn: () => adminApi.orders({ date: today }),
  });

  const s = stats.data;
  const lista = zadaci.data?.zadaci ?? [];

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-display text-2xl font-bold">Pregled</h1>
        <p className="text-muted">
          {zadaci.isLoading
            ? "Stanje za danas."
            : lista.length > 0
              ? `${lista.length} ${lista.length === 1 ? "stvar čeka" : "stvari čeka"} na tebe — najhitnije gore.`
              : "Sve je završeno."}
        </p>
      </div>

      <section aria-label="Zadaci">
        {zadaci.isLoading ? (
          <LoadingBlock />
        ) : lista.length === 0 ? (
          <div className="flex items-center gap-3 rounded-card bg-white p-5 shadow-card">
            <span className="grid h-10 w-10 place-items-center rounded-full bg-brand-50 text-brand-600">
              <CheckCircle2 className="h-5 w-5" />
            </span>
            <div>
              <p className="font-semibold">Nema otvorenih zadataka</p>
              <p className="text-sm text-muted">Narudžbe su potvrđene, korpice objavljene, zalihe na mjestu.</p>
            </div>
          </div>
        ) : (
          <ul className="grid gap-3 lg:grid-cols-2">
            {lista.map((z) => (
              <ZadatakKartica key={z.kljuc} z={z} godina={zadaci.data!.godina} sedmica={zadaci.data!.sedmica} />
            ))}
          </ul>
        )}
      </section>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Narudžbe danas" value={s?.ordersToday ?? 0} icon={<ShoppingBag className="h-4 w-4" />} accent="brand" />
        <StatCard label="Promet danas" value={formatPrice(s?.revenueToday ?? 0)} icon={<Banknote className="h-4 w-4" />} accent="honey" />
        <StatCard label="Na čekanju" value={s?.pendingOrders ?? 0} hint="čeka potvrdu" icon={<Clock className="h-4 w-4" />} accent="blue" />
        <StatCard label="U dostavi" value={s?.outForDelivery ?? 0} icon={<Truck className="h-4 w-4" />} accent="purple" />
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_18rem]">
        <section className="rounded-card bg-white p-5 shadow-card">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="font-display text-lg font-semibold">Narudžbe danas</h2>
            <Link href="/admin/orders" className="text-sm font-semibold text-brand-700 hover:underline">
              Sve narudžbe →
            </Link>
          </div>
          {orders.isLoading ? (
            <LoadingBlock />
          ) : orders.data && orders.data.length > 0 ? (
            <ul className="divide-y divide-line">
              {orders.data.map((o) => (
                <li key={o.id}>
                  <Link
                    href={`/admin/orders?pretraga=${encodeURIComponent(o.code)}`}
                    className="-mx-2 flex items-center justify-between gap-3 rounded-lg px-2 py-3 transition hover:bg-sand/60"
                  >
                    <div className="min-w-0">
                      <p className="font-medium">{o.code}</p>
                      <p className="truncate text-xs text-muted">
                        {o.customerName} · {o.deliveryZoneName} · {formatDateTime(o.createdAt)}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="font-semibold">{formatPrice(o.totalAmount)}</span>
                      <StatusBadge status={o.status} />
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="py-6 text-center text-sm text-muted">Nema narudžbi za danas.</p>
          )}
        </section>

        <section className="rounded-card bg-white p-5 shadow-card">
          <h2 className="mb-4 font-display text-lg font-semibold">Po statusu</h2>
          <ul className="space-y-1">
            {s?.statusBreakdown.map((row) => (
              <li key={row.status}>
                <Link
                  href={`/admin/orders?status=${row.status}`}
                  className="-mx-2 flex items-center justify-between rounded-lg px-2 py-1.5 text-sm transition hover:bg-sand/60"
                >
                  <StatusBadge status={row.status} />
                  <span className="font-semibold">{row.count}</span>
                </Link>
              </li>
            ))}
            {(!s || s.statusBreakdown.length === 0) && <li className="text-sm text-muted">Nema podataka.</li>}
          </ul>
        </section>
      </div>
    </div>
  );
}

function ZadatakKartica({ z, godina, sedmica }: { z: AdminZadatak; godina: number; sedmica: number }) {
  const n = NIVO[z.nivo] ?? NIVO.info;
  const qc = useQueryClient();
  const { show } = useToast();

  // Korpice bez sadržaja se mogu generisati odavde; ostalo vodi na svoj ekran.
  const generisi = useMutation({
    mutationFn: () => adminKorpiceApi.generisi({ godina, sedmica }),
    onSuccess: (rez) => {
      qc.invalidateQueries({ queryKey: ["admin", "zadaci"] });
      qc.invalidateQueries({ queryKey: ["admin", "korpice"] });
      const upozorenja = rez.filter((r) => r.upozorenje).length;
      show(
        `Nacrti za ${rez.length} korpica su spremni${upozorenja ? ` (${upozorenja} sa upozorenjem)` : ""} — pregledaj i objavi.`,
        upozorenja ? "info" : "success",
      );
    },
    onError: (err) => show(err instanceof ApiRequestError ? err.message : "Generisanje nije uspjelo", "error"),
  });
  const mozeGenerisati = z.kljuc === "sadrzaj_korpica" && z.akcija === "Generiši";

  return (
    <li className={cn("flex gap-3 rounded-card border-l-4 bg-white p-4 shadow-card", n.okvir)}>
      <span className={cn("grid h-9 w-9 shrink-0 place-items-center rounded-full", n.boja)}>
        <n.ikona className="h-4 w-4" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-[11px] font-semibold uppercase tracking-wide text-muted">{n.natpis}</p>
        <p className="font-semibold leading-snug">{z.naslov}</p>
        <p className="mt-0.5 text-sm text-muted">{z.opis}</p>
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {mozeGenerisati && (
            <button
              type="button"
              onClick={() => generisi.mutate()}
              disabled={generisi.isPending}
              className="inline-flex items-center gap-1.5 rounded-full bg-brand-600 px-3.5 py-1.5 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:opacity-60"
            >
              <Sparkles className="h-4 w-4" />
              {generisi.isPending ? "Generišem…" : "Generiši sada"}
            </button>
          )}
          <Link
            href={z.link}
            className={cn(
              "inline-flex items-center gap-1 rounded-full px-3.5 py-1.5 text-sm font-semibold transition",
              mozeGenerisati ? "text-brand-700 hover:bg-brand-50" : "bg-ink text-white hover:bg-ink/85",
            )}
          >
            {mozeGenerisati ? "Otvori korpice" : z.akcija}
            <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      </div>
    </li>
  );
}
