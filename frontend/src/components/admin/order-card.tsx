"use client";

import { ChevronDown, Phone } from "lucide-react";
import { StatusBadge } from "@/components/ui/status-badge";
import { NEXT_STATUSES, STATUS_META } from "@/lib/orders";
import { ORDER_STATUSES, type Order, type OrderStatus } from "@/types/domain";
import { cn, formatDateTime, formatPrice, unitSuffix } from "@/lib/utils";

/**
 * Order shown as a card that expands in place. The collapsed header carries the
 * key summary; expanding reveals the full order and a dropdown of valid next
 * statuses (state machine). The parent keeps `order` fresh after each mutation.
 */
export function OrderCard({
  order,
  expanded,
  pending,
  onToggle,
  onAdvance,
}: {
  order: Order;
  expanded: boolean;
  pending: boolean;
  onToggle: () => void;
  onAdvance: (status: OrderStatus) => void;
}) {
  const next = NEXT_STATUSES[order.status];

  return (
    <div className="overflow-hidden rounded-card bg-white shadow-card">
      {/* Header (click to expand) */}
      <button
        onClick={onToggle}
        aria-expanded={expanded}
        className="flex w-full items-center gap-3 p-4 text-left transition hover:bg-sand/40"
      >
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="font-display font-semibold">{order.code}</span>
            <StatusBadge status={order.status} />
          </div>
          <p className="mt-0.5 truncate text-xs text-muted">
            {order.customerName} · {order.deliveryZoneName} · {order.deliveryAddress}
          </p>
        </div>
        <div className="shrink-0 text-right">
          <p className="font-display font-bold">{formatPrice(order.totalAmount)}</p>
          <p className="text-xs text-muted">{formatDateTime(order.createdAt)}</p>
        </div>
        <ChevronDown
          className={cn(
            "h-5 w-5 shrink-0 text-muted transition-transform",
            expanded && "rotate-180",
          )}
        />
      </button>

      {/* Expanded detail */}
      {expanded && (
        <div className="space-y-5 border-t border-line p-4 sm:p-5">
          <section className="space-y-1">
            <p className="text-sm">
              <span className="font-medium">{order.customerName}</span>
              {order.customerPhone && (
                <a
                  href={`tel:${order.customerPhone}`}
                  className="ml-2 inline-flex items-center gap-1 text-brand-700 hover:underline"
                >
                  <Phone className="h-3.5 w-3.5" /> {order.customerPhone}
                </a>
              )}
            </p>
            <p className="text-sm text-muted">
              {order.deliveryZoneName} · {order.deliveryAddress}
              {order.deliverySlot?.label ? ` · ${order.deliverySlot.label}` : ""}
            </p>
          </section>

          <section className="rounded-lg bg-sand/50 p-4">
            <h3 className="mb-2 text-sm font-semibold">Stavke</h3>
            <ul className="space-y-1.5 text-sm">
              {order.items.map((it) => (
                <li key={it.id} className="flex items-center justify-between gap-3">
                  <span className="min-w-0 truncate">
                    {it.productName} × {it.quantity}
                    {unitSuffix(it.unit)}
                  </span>
                  <span className="shrink-0 text-muted">
                    {formatPrice(it.unitPrice * it.quantity)}
                  </span>
                </li>
              ))}
            </ul>
            <div className="mt-3 space-y-1 border-t border-line pt-3 text-sm">
              <div className="flex justify-between text-muted">
                <span>Međuzbir</span>
                <span>{formatPrice(order.subtotal)}</span>
              </div>
              <div className="flex justify-between text-muted">
                <span>Dostava</span>
                <span>{formatPrice(order.deliveryFee)}</span>
              </div>
              {order.popust > 0 && (
                <div className="flex justify-between text-brand-700">
                  <span>Popust{order.promoKod ? ` (${order.promoKod})` : ""}</span>
                  <span>−{formatPrice(order.popust)}</span>
                </div>
              )}
                {order.poeniPopust > 0 && (
                  <div className="flex justify-between text-brand-700">
                    <span>Poeni ({order.iskoristeniPoeni})</span>
                    <span>−{formatPrice(order.poeniPopust)}</span>
                  </div>
                )}
              <div className="flex justify-between font-display text-base font-bold">
                <span>Ukupno</span>
                <span>{formatPrice(order.totalAmount)}</span>
              </div>
            </div>
          </section>

          {order.note && (
            <p className="rounded-lg bg-honey-100 px-3 py-2 text-sm text-ink/80">
              Napomena: {order.note}
            </p>
          )}

          <div>
            <p className="mb-2 text-sm font-medium text-ink">Status narudžbe</p>
            <div className="flex flex-wrap gap-1 rounded-xl bg-sand/60 p-1">
              {ORDER_STATUSES.map((s) => {
                const isCurrent = s === order.status;
                const isValid = next.includes(s);
                return (
                  <button
                    key={s}
                    type="button"
                    disabled={pending || (!isCurrent && !isValid)}
                    onClick={() => isValid && onAdvance(s)}
                    className={cn(
                      "grow basis-24 whitespace-nowrap rounded-lg px-2.5 py-1.5 text-xs font-semibold transition",
                      isCurrent && "bg-white text-ink shadow-sm border border-line",
                      !isCurrent && isValid && "text-brand-700 hover:bg-white",
                      !isCurrent && !isValid && "cursor-not-allowed text-muted opacity-40",
                    )}
                  >
                    {isCurrent && "● "}
                    {STATUS_META[s].label}
                  </button>
                );
              })}
            </div>
            {next.length === 0 && (
              <p className="mt-1.5 text-xs text-muted">
                Narudžba je finalizovana — nema daljih promjena.
              </p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
