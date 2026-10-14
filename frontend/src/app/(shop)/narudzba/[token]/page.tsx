"use client";

import { useParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { PackageSearch } from "lucide-react";
import { OrderReceipt } from "@/components/shop/order-receipt";
import { LoadingBlock } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { buttonVariants } from "@/components/ui/button";
import { OcjenaForma } from "@/components/ocjena/ocjena-forma";
import { Zvjezdice } from "@/components/ocjena/zvjezdice";
import { orderApi } from "@/lib/api/endpoints";
import { queryKeys, useTrackOrder } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

/**
 * The permanent link from the order email — opened any time after ordering, possibly
 * days later. Unlike `/potvrda`, there is nothing to create here (`orderApi.track` is
 * a read) and nothing to thank the customer for again, so the receipt renders in
 * "pracenje" mode: the order's current status leads instead of "Hvala ti!".
 */
export default function NarudzbaPage() {
  const token = String(useParams().token);
  const qc = useQueryClient();
  const { data: order, isLoading, isError } = useTrackOrder(token);

  if (isLoading) return <LoadingBlock label="Učitavam narudžbu…" />;

  if (isError || !order) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-12">
        <EmptyState
          icon={<PackageSearch className="h-7 w-7" />}
          title="Narudžba nije pronađena"
          description="Link za praćenje je pogrešan ili je narudžba uklonjena."
          action={
            <Link href="/" className={cn(buttonVariants())}>
              Na početnu
            </Link>
          }
        />
      </div>
    );
  }

  return (
    <>
      <OrderReceipt order={order} mode="pracenje" />
      {order.mozeSeOcijeniti && (
        <section className="mx-auto mb-10 max-w-2xl px-4">
          <div className="rounded-card bg-surface p-5 shadow-card">
            <h2 className="mb-4 font-display text-lg font-bold text-ink">
              Kako je prošla isporuka?
            </h2>
            <OcjenaForma
              posalji={(i) => orderApi.ocijeniPrekoTokena(token, i)}
              onUspjeh={() => qc.invalidateQueries({ queryKey: queryKeys.trackOrder(token) })}
            />
          </div>
        </section>
      )}
      {order.ocjena && (
        <section className="mx-auto mb-10 max-w-2xl px-4">
          <div className="rounded-card bg-surface p-5 shadow-card">
            <h2 className="mb-2 font-display text-lg font-bold text-ink">Tvoja ocjena</h2>
            <Zvjezdice ocjena={order.ocjena.ocjena} velicina={24} />
            {order.ocjena.komentar && (
              <p className="mt-3 text-sm text-ink">{order.ocjena.komentar}</p>
            )}
          </div>
        </section>
      )}
    </>
  );
}
