import type {
  AuthResult,
  AuthUser,
  Category,
  DeliveryRoute,
  DeliverySlot,
  DeliveryZone,
  Order,
  OrderStatus,
  Producer,
  Product,
} from "@/types/domain";
import { apiFetch } from "./client";
import type {
  AssignDriverRequest,
  AssignRouteRequest,
  CategoryInput,
  CreateOrderRequest,
  DashboardStats,
  Driver,
  LoginRequest,
  ProducerInput,
  ProductInput,
  RegisterRequest,
  RegisterResponse,
} from "./contracts";

/**
 * Typed, resource-oriented API surface. Every consumer (shop, admin) calls
 * these instead of touching fetch/apiFetch directly, so endpoints stay in one place.
 */

/* ---------- Catalog ---------- */

export interface ProductQuery {
  category?: string;
  search?: string;
  featured?: boolean;
}

export const catalogApi = {
  products: (q: ProductQuery = {}) =>
    apiFetch<Product[]>("/products", { auth: false, query: { ...q } }).then((r) => r.data),
  product: (slug: string) =>
    apiFetch<Product>(`/products/${slug}`, { auth: false }).then((r) => r.data),
  categories: () =>
    apiFetch<Category[]>("/categories", { auth: false }).then((r) => r.data),
  producers: () =>
    apiFetch<Producer[]>("/producers", { auth: false }).then((r) => r.data),
  producer: (id: string) =>
    apiFetch<Producer>(`/producers/${id}`, { auth: false }).then((r) => r.data),
};

/* ---------- Delivery ---------- */

export const deliveryApi = {
  zones: () =>
    apiFetch<DeliveryZone[]>("/delivery/zones", { auth: false }).then((r) => r.data),
  slots: () =>
    apiFetch<DeliverySlot[]>("/delivery/slots", { auth: false }).then((r) => r.data),
};

/* ---------- Orders (customer) ---------- */

export const orderApi = {
  // auth defaults to true → the token is attached when the customer is logged in
  // (so the order links to their account); guests have no token and check out anonymously.
  create: (body: CreateOrderRequest) =>
    apiFetch<Order>("/orders", { method: "POST", body }).then((r) => r.data),
  mine: () => apiFetch<Order[]>("/orders/mine").then((r) => r.data),
  addresses: () => apiFetch<string[]>("/orders/addresses").then((r) => r.data),
  byId: (id: string) => apiFetch<Order>(`/orders/${id}`).then((r) => r.data),
};

/* ---------- Auth ---------- */

export const authApi = {
  register: (body: RegisterRequest) =>
    apiFetch<RegisterResponse>("/auth/register", { method: "POST", body, auth: false }).then(
      (r) => r.data,
    ),
  confirmEmail: (token: string) =>
    apiFetch<AuthResult>("/auth/confirm-email", {
      method: "POST",
      body: { token },
      auth: false,
    }).then((r) => r.data),
  resendConfirmation: (email: string) =>
    apiFetch<{ message: string }>("/auth/resend-confirmation", {
      method: "POST",
      body: { email },
      auth: false,
    }).then((r) => r.data),
  login: (body: LoginRequest) =>
    apiFetch<AuthResult>("/auth/login", { method: "POST", body, auth: false }).then(
      (r) => r.data,
    ),
  google: (idToken: string) =>
    apiFetch<AuthResult>("/auth/google", { method: "POST", body: { idToken }, auth: false }).then(
      (r) => r.data,
    ),
  facebook: (accessToken: string) =>
    apiFetch<AuthResult>("/auth/facebook", {
      method: "POST",
      body: { accessToken },
      auth: false,
    }).then((r) => r.data),
  me: () => apiFetch<AuthUser>("/auth/me").then((r) => r.data),
};

/* ---------- Admin ---------- */

export const adminApi = {
  dashboard: (date?: string) =>
    apiFetch<DashboardStats>("/admin/dashboard", { query: { date } }).then(
      (r) => r.data,
    ),

  /**
   * `unassigned` mijenja značenje `date`: tada je to dan DOSTAVE i vraćaju se samo narudžbe
   * koje tog dana nisu ni na čijoj ruti. Bez njega `date` je dan kreiranja narudžbe.
   */
  orders: (params: { status?: OrderStatus; date?: string; unassigned?: boolean } = {}) =>
    apiFetch<Order[]>("/admin/orders", { query: params }).then((r) => r.data),

  updateOrderStatus: (id: string, status: OrderStatus) =>
    apiFetch<Order>(`/admin/orders/${id}/status`, {
      method: "PATCH",
      body: { status },
    }).then((r) => r.data),

  products: () => apiFetch<Product[]>("/admin/products").then((r) => r.data),

  createProduct: (body: ProductInput) =>
    apiFetch<Product>("/admin/products", { method: "POST", body }).then((r) => r.data),

  updateProduct: (id: string, body: ProductInput) =>
    apiFetch<Product>(`/admin/products/${id}`, { method: "PUT", body }).then(
      (r) => r.data,
    ),

  toggleProduct: (id: string, isAvailable: boolean) =>
    apiFetch<Product>(`/admin/products/${id}/availability`, {
      method: "PATCH",
      body: { isAvailable },
    }).then((r) => r.data),

  deleteProduct: (id: string) =>
    apiFetch<string>(`/admin/products/${id}`, { method: "DELETE" }).then(() => id),

  /* Categories */
  createCategory: (body: CategoryInput) =>
    apiFetch<Category>("/admin/categories", { method: "POST", body }).then((r) => r.data),

  updateCategory: (id: string, body: CategoryInput) =>
    apiFetch<Category>(`/admin/categories/${id}`, { method: "PUT", body }).then((r) => r.data),

  deleteCategory: (id: string) =>
    apiFetch<string>(`/admin/categories/${id}`, { method: "DELETE" }).then(() => id),

  /* Producers */
  createProducer: (body: ProducerInput) =>
    apiFetch<Producer>("/admin/producers", { method: "POST", body }).then((r) => r.data),

  updateProducer: (id: string, body: ProducerInput) =>
    apiFetch<Producer>(`/admin/producers/${id}`, { method: "PUT", body }).then((r) => r.data),

  deleteProducer: (id: string) =>
    apiFetch<string>(`/admin/producers/${id}`, { method: "DELETE" }).then(() => id),

  drivers: () => apiFetch<Driver[]>("/admin/drivers").then((r) => r.data),

  routes: (date: string) =>
    apiFetch<DeliveryRoute[]>("/admin/routes", { query: { date } }).then((r) => r.data),

  // Vraća NIZ: od „više ruta po danu" jedna dodjela pravi po rutu za svaku zonu.
  assignRoute: (body: AssignRouteRequest) =>
    apiFetch<DeliveryRoute[]>("/admin/routes", { method: "POST", body }).then(
      (r) => r.data,
    ),

  /** Dodjeljuje ILI prebacuje narudžbu vozaču — isti poziv za oba, server skida staru stanicu. */
  assignOrderDriver: (orderId: string, body: AssignDriverRequest) =>
    apiFetch<DeliveryRoute[]>(`/admin/orders/${orderId}/driver`, {
      method: "POST",
      body,
    }).then((r) => r.data),

  /** Skida narudžbu sa rute; vraća se u „Nedodijeljeno". */
  removeOrderDriver: (orderId: string, date: string) =>
    apiFetch<{ message: string }>(`/admin/orders/${orderId}/driver`, {
      method: "DELETE",
      query: { date },
    }).then((r) => r.data),

  optimizeRoute: (routeId: string) =>
    apiFetch<DeliveryRoute>(`/admin/routes/${routeId}/optimize`, {
      method: "POST",
    }).then((r) => r.data),

  geocodeMissing: () =>
    // `obradjeno` = koliko je adresa ovaj prolaz pokušao (serija je ograničena, pa se dugme
    // ponavlja dok ima ostatka), `uspjesno` = koliko ih je stvarno dobilo koordinate.
    apiFetch<{ obradjeno: number; uspjesno: number }>("/admin/orders/geocode-missing", {
      method: "POST",
    }).then((r) => r.data),

  setOrderCoordinates: (id: string, latitude: number, longitude: number) =>
    apiFetch<{ message: string }>(`/admin/orders/${id}/coordinates`, {
      method: "PUT",
      body: { latitude, longitude },
    }).then((r) => r.data),
};
