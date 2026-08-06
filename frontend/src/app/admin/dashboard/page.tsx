"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Banknote, Clock, ShoppingBag, Truck } from "lucide-react";
import { StatCard } from "@/components/admin/stat-card";
import { StatusBadge } from "@/components/ui/status-badge";
import { LoadingBlock } from "@/components/ui/spinner";
import { adminApi } from "@/lib/api/endpoints";
import { STATUS_META } from "@/lib/orders";
import { formatPrice, formatDateTime } from "@/lib/utils";

export default function AdminDashboardPage() {
  const today = new Date().toISOString();
  const stats = useQuery({
    queryKey: ["admin", "dashboard"],
    queryFn: () => adminApi.dashboard(),
  });
  const orders = useQuery({
    queryKey: ["admin", "orders", "today"],
    queryFn: () => adminApi.orders({ date: today }),
  });

  if (stats.isLoading) return <LoadingBlock />;
  const s = stats.data;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-display text-2xl font-bold">Pregled</h1>
        <p className="text-muted">Stanje za danas.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Narudžbe danas"
          value={s?.ordersToday ?? 0}
          icon={<ShoppingBag className="h-4 w-4" />}
          accent="brand"
        />
        <StatCard
          label="Promet danas"
          value={formatPrice(s?.revenueToday ?? 0)}
          icon={<Banknote className="h-4 w-4" />}
          accent="honey"
        />
        <StatCard
          label="Na čekanju"
          value={s?.pendingOrders ?? 0}
          hint="čeka potvrdu"
          icon={<Clock className="h-4 w-4" />}
          accent="blue"
        />
        <StatCard
          label="U dostavi"
          value={s?.outForDelivery ?? 0}
          icon={<Truck className="h-4 w-4" />}
          accent="purple"
        />
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
                <li key={o.id} className="flex items-center justify-between gap-3 py-3">
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
                </li>
              ))}
            </ul>
          ) : (
            <p className="py-6 text-center text-sm text-muted">Nema narudžbi za danas.</p>
          )}
        </section>

        <section className="rounded-card bg-white p-5 shadow-card">
          <h2 className="mb-4 font-display text-lg font-semibold">Po statusu</h2>
          <ul className="space-y-2">
            {s?.statusBreakdown.map((row) => (
              <li key={row.status} className="flex items-center justify-between text-sm">
                <StatusBadge status={row.status} />
                <span className="font-semibold">{row.count}</span>
              </li>
            ))}
            {(!s || s.statusBreakdown.length === 0) && (
              <li className="text-sm text-muted">Nema podataka.</li>
            )}
          </ul>
          <p className="mt-4 text-xs text-muted">
            Ukupno statusa: {STATUS_META ? Object.keys(STATUS_META).length : 0} mogućih stanja.
          </p>
        </section>
      </div>
    </div>
  );
}
