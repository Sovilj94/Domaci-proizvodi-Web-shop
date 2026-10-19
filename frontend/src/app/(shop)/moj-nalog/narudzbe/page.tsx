"use client";

import { useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { CalendarClock, MapPin, PackageOpen } from "lucide-react";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/status-badge";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { useAuth } from "@/lib/auth/auth-context";
import { OcjenaForma } from "@/components/ocjena/ocjena-forma";
import { Zvjezdice } from "@/components/ocjena/zvjezdice";
import { orderApi } from "@/lib/api/endpoints";
import { queryKeys, useMyOrders } from "@/lib/api/hooks";
import { formatDateTime, formatPrice, unitSuffix } from "@/lib/utils";

export default function OrdersPage() {
  const { user } = useAuth();
  const orders = useMyOrders(!!user);
  const qc = useQueryClient();
  const [otvorena, setOtvorena] = useState<string | null>(null);

  /*
    Ekran je prenesen iz `/orders` bez redizajna, ali je tamo bio samostalna stranica i nosio
    je svoj `mx-auto max-w-3xl px-4 py-8`. Unutar ljuske naloga, koja već daje okvir i razmak,
    to je značilo dupli padding i užu kolonu pomjerenu u odnosu na ostale tri sekcije.
  */
  return (
    <div>
      <h1 className="font-display text-2xl font-bold text-ink md:text-3xl">Moje narudžbe</h1>

      <div className="mt-6">
        {orders.isLoading ? (
          <LoadingBlock />
        ) : orders.data && orders.data.length > 0 ? (
          <ul className="space-y-4">
            {orders.data.map((o) => (
              <li key={o.id} className="rounded-card bg-surface p-5 shadow-card">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div>
                    <p className="font-display font-semibold">{o.code}</p>
                    <p className="text-xs text-muted">{formatDateTime(o.createdAt)}</p>
                  </div>
                  <StatusBadge status={o.status} />
                </div>

                <ul className="mt-4 space-y-1 text-sm">
                  {o.items.map((it) => (
                    <li key={it.id} className="flex justify-between gap-2">
                      <span className="text-muted">
                        {it.productName} × {it.quantity}
                        {unitSuffix(it.unit)}
                      </span>
                      <span className="font-medium">
                        {formatPrice(it.unitPrice * it.quantity)}
                      </span>
                    </li>
                  ))}
                </ul>

                <div className="mt-4 flex flex-wrap gap-x-5 gap-y-1 border-t border-line pt-3 text-xs text-muted">
                  <span className="flex items-center gap-1">
                    <CalendarClock className="h-3.5 w-3.5" /> {o.deliverySlot?.label}
                  </span>
                  <span className="flex items-center gap-1">
                    <MapPin className="h-3.5 w-3.5" /> {o.deliveryZoneName} · {o.deliveryAddress}
                  </span>
                </div>

                <div className="mt-3 flex items-center justify-between">
                  <span className="text-sm text-muted">Ukupno</span>
                  <span className="font-display text-lg font-bold">
                    {formatPrice(o.totalAmount)}
                  </span>
                </div>

                {o.ocjena && (
                  <div className="mt-3 flex items-center gap-2 border-t border-line pt-3">
                    <span className="text-sm text-muted">Tvoja ocjena</span>
                    <Zvjezdice ocjena={o.ocjena.ocjena} />
                  </div>
                )}
                {o.mozeSeOcijeniti && (
                  <div className="mt-3 border-t border-line pt-3">
                    {otvorena === o.id ? (
                      <OcjenaForma
                        posalji={(i) => orderApi.ocijeni(o.id, i)}
                        onUspjeh={() => qc.invalidateQueries({ queryKey: queryKeys.myOrders() })}
                      />
                    ) : (
                      <Button variant="secondary" onClick={() => setOtvorena(o.id)}>
                        Ocijeni isporuku
                      </Button>
                    )}
                  </div>
                )}
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState
            icon={<PackageOpen className="h-7 w-7" />}
            title="Još nemate narudžbi"
            description="Kada naručiš, ovdje ćeš pratiti status dostave."
            action={
              <Link href="/spajz">
                <Button>Pogledaj proizvode</Button>
              </Link>
            }
          />
        )}
      </div>
    </div>
  );
}
