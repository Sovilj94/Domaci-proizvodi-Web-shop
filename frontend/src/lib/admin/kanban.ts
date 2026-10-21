import type { Order, OrderStatus } from "@/types/domain";
import { NEXT_STATUSES } from "../orders.ts";

/** Kolone table, redom kojim narudžba prolazi. Otkazane i neuspješne nisu kolone — vidi ispod. */
export const KOLONE: OrderStatus[] = ["Pending", "Confirmed", "Packing", "OutForDelivery", "Delivered"];

/** Završene kolone se skraćuju: hronika, ne posao. */
export const NAJVISE_U_ZAVRSENOJ = 15;

/** Smije li se kartica prevući iz `od` u `u` — isti prelazi kao Order.Transitions na backendu. */
export function mozePreci(od: OrderStatus, u: OrderStatus): boolean {
  return od !== u && NEXT_STATUSES[od].includes(u);
}

/** Sljedeći korak „naprijed" za dugme na kartici (bez otkazivanja i neuspjeha). */
export function sljedeci(od: OrderStatus): OrderStatus | null {
  return NEXT_STATUSES[od].find((s) => s !== "Cancelled" && s !== "FailedDelivery") ?? null;
}

export interface Tabla {
  kolone: Record<OrderStatus, Order[]>;
  /** Neuspješne dostave traže odluku (ponovo u dostavu ili otkaži) — idu u traku iznad table. */
  problemi: Order[];
  otkazane: number;
}

/** Narudžbe po kolonama; unutar kolone najstarije prve (red čekanja), završene najnovije prve. */
export function rasporedi(narudzbe: Order[]): Tabla {
  const kolone = Object.fromEntries(KOLONE.map((k) => [k, [] as Order[]])) as unknown as Record<OrderStatus, Order[]>;
  const problemi: Order[] = [];
  let otkazane = 0;
  for (const o of narudzbe) {
    if (o.status === "FailedDelivery") problemi.push(o);
    else if (o.status === "Cancelled") otkazane++;
    else kolone[o.status].push(o);
  }
  for (const k of KOLONE) {
    kolone[k].sort((a, b) =>
      k === "Delivered" ? b.createdAt.localeCompare(a.createdAt) : a.createdAt.localeCompare(b.createdAt),
    );
  }
  return { kolone, problemi, otkazane };
}
