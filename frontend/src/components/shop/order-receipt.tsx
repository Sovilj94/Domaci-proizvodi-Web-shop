import Link from "next/link";
import { CheckCircle2, Mail } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { StatusBadge } from "@/components/ui/status-badge";
import { buttonVariants } from "@/components/ui/button";
import { useAuth } from "@/lib/auth/auth-context";
import type { Order } from "@/types/domain";
import { cn, formatPrice, unitSuffix } from "@/lib/utils";

/**
 * One screen for three visitors: a logged-in customer right after ordering, a guest
 * whose click on the emailed link just created the order, and anyone opening the
 * permanent tracking link days later. `mode` is the only thing that changes the
 * framing — "zahvala" is a moment of thanks, "pracenje" leads with the order's
 * current status instead, since by then the customer wants to know where it is, not
 * be thanked again.
 *
 * The "Moje narudžbe" link only makes sense for someone who has an account to look
 * it up in, so it reads `useAuth()` itself rather than taking a `loggedIn` prop —
 * a guest reaching this screen (either right after confirming, or via a tracking
 * link) never gets a link to a page that would be empty for them.
 */
export function OrderReceipt({ order, mode }: { order: Order; mode: "zahvala" | "pracenje" }) {
  const { user } = useAuth();

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 md:py-12">
      <div className="lg:grid lg:grid-cols-[minmax(0,1fr)_320px] lg:items-start lg:gap-8">
        <div className="space-y-6">
          <div>
            {mode === "zahvala" ? (
              <>
                <div className="grid h-14 w-14 place-items-center rounded-full bg-brand-100 text-brand-600">
                  <CheckCircle2 className="h-8 w-8" />
                </div>
                <h1 className="mt-4 font-display text-2xl font-bold md:text-3xl">Hvala ti!</h1>
                <p className="mt-1 text-muted">
                  Broj narudžbe <span className="font-semibold text-ink">{order.code}</span>
                </p>
              </>
            ) : (
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="font-display text-2xl font-bold md:text-3xl">{order.code}</h1>
                <StatusBadge status={order.status} />
              </div>
            )}

            {mode === "zahvala" && order.customerEmail && (
              <p className="mt-3 inline-flex items-center gap-2 rounded-full bg-honey-100 px-4 py-2 text-sm">
                <Mail className="h-4 w-4 text-honey-600" />
                Potvrda je poslana na{" "}
                <span className="font-medium">{order.customerEmail}</span>.
              </p>
            )}
          </div>

          {/* Narudžba */}
          <section className="rounded-card bg-surface p-5 shadow-card">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 className="font-display text-lg font-semibold">Narudžba</h2>
              <span className="text-sm text-muted">
                {order.deliverySlot?.label ?? "Termin dostave"}
              </span>
            </div>
            <ul className="mt-4 divide-y divide-line">
              {order.items.map((it) => (
                <li key={it.id} className="flex items-center gap-3 py-3">
                  <div className="relative h-14 w-14 shrink-0 overflow-hidden rounded-lg">
                    <SmartImage src={it.imageUrl} alt={it.productName} sizes="56px" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium">{it.productName}</p>
                    <p className="text-sm text-muted">
                      {it.quantity}
                      {unitSuffix(it.unit)} × {formatPrice(it.unitPrice)}
                    </p>
                  </div>
                  <span className="shrink-0 font-semibold">
                    {formatPrice(it.unitPrice * it.quantity)}
                  </span>
                </li>
              ))}
            </ul>
          </section>

          {/* Podaci o dostavi */}
          <section className="rounded-card bg-surface p-5 shadow-card text-sm">
            <h2 className="font-display text-lg font-semibold">Podaci o dostavi</h2>
            <dl className="mt-3 space-y-1.5">
              <div className="flex justify-between gap-4">
                <dt className="text-muted">Ime</dt>
                <dd className="text-right font-medium">{order.customerName}</dd>
              </div>
              {order.customerPhone && (
                <div className="flex justify-between gap-4">
                  <dt className="text-muted">Telefon</dt>
                  <dd className="text-right font-medium">{order.customerPhone}</dd>
                </div>
              )}
              <div className="flex justify-between gap-4">
                <dt className="text-muted">Adresa</dt>
                <dd className="max-w-[65%] text-right font-medium">{order.deliveryAddress}</dd>
              </div>
              {order.note && (
                <div className="flex justify-between gap-4">
                  <dt className="text-muted">Napomena</dt>
                  <dd className="max-w-[65%] text-right font-medium">{order.note}</dd>
                </div>
              )}
            </dl>
          </section>

          {/* Način plaćanja */}
          <section className="rounded-card bg-surface p-5 shadow-card">
            <h2 className="font-display text-lg font-semibold">Način plaćanja</h2>
            <p className="mt-2 text-sm font-medium text-brand-700">Pouzećem pri dostavi</p>
          </section>
        </div>

        <aside className="mt-6 space-y-4 lg:sticky lg:top-24 lg:mt-0">
          <div className="rounded-card bg-surface p-5 shadow-card">
            <h2 className="font-display text-base font-semibold">Ukupna cijena</h2>
            <dl className="mt-3 space-y-2 border-t border-line pt-3 text-sm">
              <div className="flex justify-between">
                <dt className="text-muted">Iznos stavki</dt>
                <dd className="font-medium">{formatPrice(order.subtotal)}</dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-muted">Trošak dostave</dt>
                <dd className="font-medium">{formatPrice(order.deliveryFee)}</dd>
              </div>
              {order.popust > 0 && (
                <div className="flex justify-between text-brand-700">
                  <dt>Popust{order.promoKod ? ` (${order.promoKod})` : ""}</dt>
                  <dd className="font-medium">−{formatPrice(order.popust)}</dd>
                </div>
              )}
              {order.poeniPopust > 0 && (
                <div className="flex justify-between text-brand-700">
                  <dt>Poeni ({order.iskoristeniPoeni})</dt>
                  <dd className="font-medium">−{formatPrice(order.poeniPopust)}</dd>
                </div>
              )}
              <div className="flex justify-between border-t border-line pt-2 text-base">
                <dt className="font-semibold">Ukupno</dt>
                <dd className="font-display font-bold">{formatPrice(order.totalAmount)}</dd>
              </div>
            </dl>
          </div>

          <div className="flex flex-col gap-3">
            <Link href="/products" className={cn(buttonVariants({ variant: "secondary" }))}>
              Nastavi kupovinu
            </Link>
            {user && (
              <Link href="/moj-nalog/narudzbe" className={cn(buttonVariants())}>
                Moje narudžbe
              </Link>
            )}
          </div>
        </aside>
      </div>
    </div>
  );
}
