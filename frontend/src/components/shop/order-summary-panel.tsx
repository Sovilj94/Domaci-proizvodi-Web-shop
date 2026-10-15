import type { CartItem } from "@/lib/cart/cart-context";
import { formatPrice, unitSuffix } from "@/lib/utils";

/**
 * Order summary shown beside the checkout form (sticky sidebar on wide screens,
 * inline above the step buttons on narrow ones — the caller decides placement,
 * this just renders the content once so both spots stay in sync).
 */
export function OrderSummaryPanel({
  items,
  subtotal,
  deliveryFee,
  total,
  zoneName,
  popust = 0,
  promoKod,
}: {
  items: CartItem[];
  subtotal: number;
  deliveryFee: number;
  total: number;
  zoneName?: string;
  popust?: number;
  promoKod?: string;
}) {
  return (
    <div className="space-y-4 rounded-card bg-surface p-5 shadow-card">
      <h2 className="font-display text-base font-semibold">Vaša narudžba</h2>

      <ul className="space-y-2 text-sm">
        {items.map((i) => (
          <li key={i.productId} className="flex justify-between gap-2">
            <span className="min-w-0 truncate text-muted">
              {i.name} × {i.quantity}
              {unitSuffix(i.unit)}
            </span>
            <span className="shrink-0 font-medium">{formatPrice(i.price * i.quantity)}</span>
          </li>
        ))}
      </ul>

      <dl className="space-y-2 border-t border-line pt-4 text-sm">
        <div className="flex justify-between">
          <dt className="text-muted">Međuzbir</dt>
          <dd className="font-medium">{formatPrice(subtotal)}</dd>
        </div>
        <div className="flex justify-between">
          <dt className="text-muted">Dostava{zoneName ? ` (${zoneName})` : ""}</dt>
          <dd className="font-medium">{formatPrice(deliveryFee)}</dd>
        </div>
        {popust > 0 && (
          <div className="flex justify-between text-brand-700">
            <dt>Popust{promoKod ? ` (${promoKod})` : ""}</dt>
            <dd className="font-medium">−{formatPrice(popust)}</dd>
          </div>
        )}
        <div className="flex justify-between border-t border-line pt-2 text-base">
          <dt className="font-semibold">Ukupno</dt>
          <dd className="font-display font-bold">{formatPrice(total)}</dd>
        </div>
      </dl>
    </div>
  );
}
