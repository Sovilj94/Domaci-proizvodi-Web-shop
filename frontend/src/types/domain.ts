/**
 * Domain contracts shared by the whole platform (web shop, admin, driver app).
 * These mirror the ASP.NET Core DTOs 1:1 so the same shapes travel over the wire.
 */

export type Unit = "kg" | "kom" | "l";

export const ORDER_STATUSES = [
  "Pending",
  "Confirmed",
  "Packing",
  "OutForDelivery",
  "Delivered",
  "FailedDelivery",
  "Cancelled",
] as const;
export type OrderStatus = (typeof ORDER_STATUSES)[number];

export type RouteStatus = "Planned" | "Optimized" | "InProgress" | "Completed";

export type UserRole = "customer" | "admin" | "driver";

export interface Category {
  id: string;
  name: string;
  slug: string;
  imageUrl?: string;
  sortOrder: number;
  productCount?: number;
}

export interface Producer {
  id: string;
  name: string;
  village: string;
  description: string;
  photoUrl?: string;
  latitude?: number;
  longitude?: number;
  phone?: string;
}

/** Lightweight producer summary embedded on a product. */
export interface ProducerRef {
  id: string;
  name: string;
  village: string;
  photoUrl?: string;
}

export interface Product {
  id: string;
  name: string;
  slug: string;
  price: number;
  unit: Unit;
  stock: number;
  isAvailable: boolean;
  categoryId: string;
  categoryName: string;
  categorySlug: string;
  producerId: string;
  producer?: ProducerRef;
  imageUrl?: string;
  description: string;
  isFeatured?: boolean;
}

export interface DeliveryZone {
  id: string;
  name: string;
  minOrderAmount: number;
  deliveryFee: number;
  isActive: boolean;
}

export interface DeliverySlot {
  id: string;
  dayOfWeek: number; // 0 = Sunday .. 6 = Saturday
  timeFrom: string; // "16:00"
  timeTo: string; // "20:00"
  cutoffHours: number;
  /** Human label the API may precompute, e.g. "Četvrtak 16–20h". */
  label?: string;
}

export interface OrderItem {
  id: string;
  productId: string;
  productName: string;
  unit: Unit;
  quantity: number;
  /** Price snapshot at time of order — never changes afterwards. */
  unitPrice: number;
  imageUrl?: string;
}

export interface Order {
  id: string;
  code: string; // human-friendly, e.g. "EP-2601-0042"
  customerId: string;
  customerName: string;
  customerPhone?: string;
  status: OrderStatus;
  items: OrderItem[];
  subtotal: number;
  deliveryFee: number;
  totalAmount: number;
  deliverySlotId: string;
  deliverySlot?: DeliverySlot;
  deliveryZoneId: string;
  deliveryZoneName?: string;
  deliveryAddress: string;
  note?: string;
  createdAt: string;
}

export interface RouteStop {
  orderId: string;
  orderCode: string;
  customerName: string;
  address: string;
  zoneName: string;
  sequence: number;
  status?: string;
  customerPhone?: string;
  latitude?: number;
  longitude?: number;
}

export interface DeliveryRoute {
  id: string;
  date: string;
  driverId?: string;
  driverName?: string;
  status: RouteStatus;
  stops: RouteStop[];
}

export interface Customer {
  id: string;
  name: string;
  email: string;
  phone: string;
  address: string;
  deliveryZoneId?: string;
}

export interface AuthUser {
  id: string;
  name: string;
  email: string;
  phone?: string;
  address?: string;
  deliveryZoneId?: string;
  role: UserRole;
}

export interface AuthResult {
  token: string;
  user: AuthUser;
}

/**
 * Polazište i krajnja tačka svake ture, jedna vrijednost za cijelu piljaru.
 * Koordinate nisu opcione — bez njih se ruta ne bi mogla računati, pa ih backend
 * odbija i na `PUT`-u i na startu.
 */
export interface Warehouse {
  name: string;
  latitude: number;
  longitude: number;
}
