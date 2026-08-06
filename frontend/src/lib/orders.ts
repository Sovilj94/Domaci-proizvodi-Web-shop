import type { OrderStatus } from "@/types/domain";

interface StatusMeta {
  label: string;
  /** Tailwind classes for a soft badge. */
  badge: string;
  /** Solid dot colour. */
  dot: string;
}

export const STATUS_META: Record<OrderStatus, StatusMeta> = {
  Pending: { label: "Na čekanju", badge: "bg-honey-100 text-honey-600", dot: "bg-honey-500" },
  Confirmed: { label: "Potvrđeno", badge: "bg-blue-100 text-blue-700", dot: "bg-blue-500" },
  Packing: { label: "Pakovanje", badge: "bg-indigo-100 text-indigo-700", dot: "bg-indigo-500" },
  OutForDelivery: { label: "U dostavi", badge: "bg-purple-100 text-purple-700", dot: "bg-purple-500" },
  Delivered: { label: "Dostavljeno", badge: "bg-brand-100 text-brand-700", dot: "bg-brand-500" },
  FailedDelivery: { label: "Neuspješna dostava", badge: "bg-red-100 text-red-700", dot: "bg-red-500" },
  Cancelled: { label: "Otkazano", badge: "bg-zinc-200 text-zinc-600", dot: "bg-zinc-400" },
};

/** Allowed forward transitions used by the admin status controls. */
export const NEXT_STATUSES: Record<OrderStatus, OrderStatus[]> = {
  Pending: ["Confirmed", "Cancelled"],
  Confirmed: ["Packing", "Cancelled"],
  Packing: ["OutForDelivery"],
  OutForDelivery: ["Delivered", "FailedDelivery"],
  Delivered: [],
  FailedDelivery: ["OutForDelivery", "Cancelled"],
  Cancelled: [],
};
