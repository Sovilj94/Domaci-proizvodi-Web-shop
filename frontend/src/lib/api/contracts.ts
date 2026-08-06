import type { OrderStatus, Unit } from "@/types/domain";

/** Request/response payloads that are not plain domain entities. */

export interface CreateOrderItemInput {
  productId: string;
  quantity: number;
}

export interface CreateOrderRequest {
  customer: {
    name: string;
    email: string;
    phone: string;
  };
  deliveryZoneId: string;
  deliverySlotId: string;
  deliveryAddress: string;
  note?: string;
  items: CreateOrderItemInput[];
}

export interface RegisterRequest {
  name: string;
  email: string;
  phone: string;
  password: string;
  address?: string;
  deliveryZoneId?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

/** Registration no longer logs in — the user must confirm their email first. */
export interface RegisterResponse {
  email: string;
  confirmationRequired: boolean;
}

export interface ProductInput {
  name: string;
  price: number;
  unit: Unit;
  stock: number;
  categoryId: string;
  producerId: string;
  description: string;
  imageUrl?: string;
  isAvailable: boolean;
  isFeatured?: boolean;
}

export interface CategoryInput {
  name: string;
  imageUrl?: string;
  sortOrder: number;
}

export interface ProducerInput {
  name: string;
  village: string;
  description?: string;
  photoUrl?: string;
  phone?: string;
  latitude?: number;
  longitude?: number;
}

export interface AssignRouteRequest {
  date: string;
  driverId: string;
  orderIds: string[];
}

export interface DashboardStats {
  date: string;
  ordersToday: number;
  revenueToday: number;
  pendingOrders: number;
  outForDelivery: number;
  statusBreakdown: { status: OrderStatus; count: number }[];
}

export interface Driver {
  id: string;
  name: string;
  phone: string;
}
