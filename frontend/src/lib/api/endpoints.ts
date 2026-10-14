import type {
  Address,
  AuthResult,
  AuthUser,
  Category,
  DeliveryRoute,
  DeliverySlot,
  DeliveryZone,
  GeocodeHit,
  Korpica,
  MojaKorpica,
  Order,
  OrderStatus,
  Pretplata,
  Producer,
  Product,
  SadrzajKorpice,
  Sedmica,
  TipKorpice,
  Utisak,
  Warehouse,
} from "@/types/domain";
import { apiFetch } from "./client";
import type {
  AddressInput,
  AssignDriverRequest,
  AssignOrdersBulkRequest,
  AssignRouteRequest,
  CategoryInput,
  CreateOrderRequest,
  DashboardStats,
  Driver,
  DriverInput,
  GenerisiSadrzajRequest,
  GuestOrderRequestResult,
  IzmijeniNacrtRequest,
  LoginRequest,
  MojaKorpicaDetalji,
  PakerskaLista,
  PretplataInput,
  ProducerInput,
  ProductInput,
  ProfileInput,
  RegisterRequest,
  RegisterResponse,
  RezultatGenerisanja,
  SadrzajPregled,
  UtisakInput,
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

/* ---------- Korpice ---------- */

export const korpiceApi = {
  lista: (tip?: TipKorpice) =>
    apiFetch<Korpica[]>("/korpice", {
      auth: false,
      query: tip ? { tip } : {},
    }).then((r) => r.data),

  korpica: (slug: string) =>
    apiFetch<Korpica>(`/korpice/${slug}`, { auth: false }).then((r) => r.data),

  sadrzaj: (slug: string) =>
    apiFetch<SadrzajKorpice>(`/korpice/${slug}/sadrzaj`, { auth: false }).then((r) => r.data),

  sedmica: () => apiFetch<Sedmica>("/korpice/sedmica", { auth: false }).then((r) => r.data),
};

/* ---------- Utisci ---------- */

export const utisciApi = {
  lista: () => apiFetch<Utisak[]>("/utisci", { auth: false }).then((r) => r.data),
};

/* ---------- Admin: korpice ---------- */

export const adminKorpiceApi = {
  pregled: (godina: number, sedmica: number) =>
    apiFetch<SadrzajPregled[]>("/admin/korpice/sadrzaj", {
      query: { godina, sedmica },
    }).then((r) => r.data),

  /** Jedan sadržaj, uključujući nacrt — javni endpoint nacrt odbija. */
  sadrzaj: (id: string) =>
    apiFetch<SadrzajKorpice>(`/admin/korpice/sadrzaj/${id}`).then((r) => r.data),

  generisi: (body: GenerisiSadrzajRequest) =>
    apiFetch<RezultatGenerisanja[]>("/admin/korpice/sadrzaj/generisi", {
      method: "POST",
      body,
    }).then((r) => r.data),

  izmijeni: (id: string, body: IzmijeniNacrtRequest) =>
    apiFetch<SadrzajKorpice>(`/admin/korpice/sadrzaj/${id}`, {
      method: "PUT",
      body,
    }).then((r) => r.data),

  objavi: (id: string) =>
    apiFetch<SadrzajKorpice>(`/admin/korpice/sadrzaj/${id}/objavi`, {
      method: "POST",
    }).then((r) => r.data),

  pakerskaLista: (godina: number, sedmica: number) =>
    apiFetch<PakerskaLista>("/admin/korpice/pakerska-lista", {
      query: { godina, sedmica },
    }).then((r) => r.data),
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

  /* Gost: narudžba nastaje tek na potvrdu iz mejla. */
  guestRequest: (body: CreateOrderRequest) =>
    apiFetch<GuestOrderRequestResult>("/orders/guest-request", { method: "POST", body }).then(
      (r) => r.data,
    ),

  confirmGuest: (token: string) =>
    apiFetch<Order>("/orders/confirm", { method: "POST", body: { token } }).then((r) => r.data),

  track: (token: string) =>
    apiFetch<Order>(`/orders/track/${token}`).then((r) => r.data),
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

/* ---------- Nalog ---------- */

export const accountApi = {
  adrese: () => apiFetch<Address[]>("/account/addresses").then((r) => r.data),

  dodajAdresu: (body: AddressInput) =>
    apiFetch<Address>("/account/addresses", { method: "POST", body }).then((r) => r.data),

  izmijeniAdresu: (id: string, body: AddressInput) =>
    apiFetch<Address>(`/account/addresses/${id}`, { method: "PUT", body }).then((r) => r.data),

  /** Backend vraća 204 bez tijela; `apiFetch` to prepoznaje kao uspjeh (vidi client.ts). */
  obrisiAdresu: (id: string): Promise<void> =>
    apiFetch<void>(`/account/addresses/${id}`, { method: "DELETE" }).then(() => undefined),

  postaviGlavnu: (id: string) =>
    apiFetch<Address>(`/account/addresses/${id}/default`, { method: "POST" }).then(
      (r) => r.data,
    ),

  azurirajProfil: (body: ProfileInput) =>
    apiFetch<AuthUser>("/account/profile", { method: "PUT", body }).then((r) => r.data),

  mojeKorpice: () => apiFetch<MojaKorpica[]>("/account/korpice").then((r) => r.data),

  mojaKorpica: (orderItemId: string) =>
    apiFetch<MojaKorpicaDetalji>(`/account/korpice/${orderItemId}`).then((r) => r.data),

  izmijeniKorpicu: (orderItemId: string, productIds: string[]) =>
    apiFetch<MojaKorpicaDetalji>(`/account/korpice/${orderItemId}`, {
      method: "PUT",
      body: { productIds },
    }).then((r) => r.data),

  pretplate: () => apiFetch<Pretplata[]>("/account/pretplate").then((r) => r.data),

  dodajPretplatu: (body: PretplataInput) =>
    apiFetch<Pretplata>("/account/pretplate", { method: "POST", body }).then((r) => r.data),

  izmijeniPretplatu: (id: string, body: PretplataInput) =>
    apiFetch<Pretplata>(`/account/pretplate/${id}`, { method: "PUT", body }).then(
      (r) => r.data,
    ),

  /** `sedmica` je broj ISPORUKA koje se preskaču, ne datum — vidi spec P3, odluka O4. */
  pauzirajPretplatu: (id: string, sedmica: number) =>
    apiFetch<Pretplata>(`/account/pretplate/${id}/pauza`, {
      method: "POST",
      body: { sedmica },
    }).then((r) => r.data),

  nastaviPretplatu: (id: string) =>
    apiFetch<Pretplata>(`/account/pretplate/${id}/nastavi`, { method: "POST" }).then(
      (r) => r.data,
    ),

  /** Backend vraća 204 bez tijela; `apiFetch` to prepoznaje kao uspjeh (vidi client.ts). */
  otkaziPretplatu: (id: string): Promise<void> =>
    apiFetch<void>(`/account/pretplate/${id}`, { method: "DELETE" }).then(() => undefined),
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

  /* Utisci */
  utisci: () => apiFetch<Utisak[]>("/admin/utisci").then((r) => r.data),

  createUtisak: (body: UtisakInput) =>
    apiFetch<Utisak>("/admin/utisci", { method: "POST", body }).then((r) => r.data),

  updateUtisak: (id: string, body: UtisakInput) =>
    apiFetch<Utisak>(`/admin/utisci/${id}`, { method: "PUT", body }).then((r) => r.data),

  deleteUtisak: (id: string) =>
    apiFetch<string>(`/admin/utisci/${id}`, { method: "DELETE" }).then(() => id),

  /* Drivers */
  drivers: () => apiFetch<Driver[]>("/admin/drivers").then((r) => r.data),

  createDriver: (body: DriverInput) =>
    apiFetch<Driver>("/admin/drivers", { method: "POST", body }).then((r) => r.data),

  updateDriver: (id: string, body: DriverInput) =>
    apiFetch<Driver>(`/admin/drivers/${id}`, { method: "PUT", body }).then((r) => r.data),

  deleteDriver: (id: string) =>
    apiFetch<string>(`/admin/drivers/${id}`, { method: "DELETE" }).then(() => id),

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

  /** Dodjeljuje cijelo naselje jednom vozaču u jednom zahtjevu — sve ili ništa. */
  assignOrdersBulk: (body: AssignOrdersBulkRequest) =>
    apiFetch<DeliveryRoute[]>("/admin/orders/assign-bulk", {
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

  warehouse: () => apiFetch<Warehouse>("/admin/warehouse").then((r) => r.data),

  updateWarehouse: (body: Warehouse) =>
    apiFetch<Warehouse>("/admin/warehouse", { method: "PUT", body }).then((r) => r.data),
};

/* ---------- Geocoding (kroz naš backend, ne direktno na Nominatim) ---------- */

/*
 * `?? null`, a ne samo `r.data`: backend serijalizuje sa WhenWritingNull, pa „nema
 * pogotka" na žici izgleda kao `{}` i `data` stigne kao `undefined`. Bez ovoga bi tip
 * obećavao `null` a vraćao `undefined`.
 */
export const geocodeApi = {
  /** Adresa → tačka. `null` kad nema pogotka; to nije greška. */
  search: (q: string) =>
    apiFetch<GeocodeHit | null>("/geocode", { auth: false, query: { q } }).then(
      (r) => r.data ?? null,
    ),

  /** Tačka → adresa. `null` kad nema pogotka. */
  reverse: (lat: number, lon: number) =>
    apiFetch<GeocodeHit | null>("/geocode/reverse", {
      auth: false,
      query: { lat: String(lat), lon: String(lon) },
    }).then((r) => r.data ?? null),
};
