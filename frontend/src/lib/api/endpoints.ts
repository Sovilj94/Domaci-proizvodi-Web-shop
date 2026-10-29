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
  OcjenaIsporuke,
  OcjeneAdmin,
  Order,
  OrderStatus,
  MojiPoeni,
  Pretplata,
  Producer,
  Product,
  SadrzajKorpice,
  SezonaKorpice,
  AdminSadrzajKorpice,
  Sedmica,
  TipKorpice,
  Utisak,
  PromoKod,
  Analiza,
  PartnerPrijava,
  StatusPrijave,
  Recept,
  ReceptAdmin,
  ReceptKratko,
  Warehouse,
} from "@/types/domain";
import { apiFetch } from "./client";

/** Folder slike u bazi (`/slike/{folder}/…`) — prati backend `SlikePravila.Folderi`. */
export type FolderSlike = "products" | "categories" | "producers" | "korpice" | "recepti";
import type {
  AdminKupac,
  AdminKupacDetalj,
  AdminPoeniKupac,
  AdminPoeniKupca,
  AdminPretplate,
  Pakovanje,
  Pitanje,
  SadrzajSajta,
} from "@/types/domain";
import { pismoNaZici, type Pismo } from "@/lib/pismo/pismo";
import { uLatinicu } from "@/lib/pismo/preslovi";
import type {
  AddressInput,
  AssignDriverRequest,
  AssignOrdersBulkRequest,
  AssignRouteRequest,
  CategoryInput,
  CreateOrderRequest,
  AdminKorpicaNarudzbe,
  AdminZadaci,
  IzbjegnutArtikal,
  DashboardStats,
  Driver,
  DriverInput,
  GenerisiSadrzajRequest,
  GuestOrderRequestResult,
  IzmijeniNacrtRequest,
  LoginRequest,
  MojaKorpicaDetalji,
  OcjenaInput,
  PakerskaLista,
  PretplataInput,
  ProvjeraPromoKoda,
  ProducerInput,
  ProductInput,
  ProfileInput,
  RegisterRequest,
  RegisterResponse,
  RezultatGenerisanja,
  SadrzajPregled,
  UtisakInput,
  PromoKodInput,
  AnalizaInput,
  PartnerPrijavaInput,
  PartnerStatusInput,
  ReceptInput,
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
  /** id proizvođača — stranica domaćina */
  producer?: string;
}

export const catalogApi = {
  // Nazivi u bazi su latinični; upit kucan ćirilicom se vraća u latinicu (P7).
  products: (q: ProductQuery = {}) =>
    apiFetch<Product[]>("/products", {
      auth: false,
      query: { ...q, search: q.search ? uLatinicu(q.search) : q.search },
    }).then((r) => r.data),
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

  sezona: (tip: TipKorpice) =>
    apiFetch<SezonaKorpice>("/korpice/sezona", { auth: false, query: { tip } }).then((r) => r.data),

  sedmica: () => apiFetch<Sedmica>("/korpice/sedmica", { auth: false }).then((r) => r.data),
  /** Kontakt i česta pitanja koje admin uređuje; null polje = ugrađeni tekst. */
  sadrzajSajta: () => apiFetch<SadrzajSajta>("/sadrzaj-sajta", { auth: false }).then((r) => r.data),
};

/* ---------- Utisci ---------- */

export const utisciApi = {
  lista: () => apiFetch<Utisak[]>("/utisci", { auth: false }).then((r) => r.data),
};

/* ---------- Analize ---------- */

export const analizeApi = {
  lista: (producer?: string) =>
    apiFetch<Analiza[]>("/analize", { auth: false, query: { producer } }).then((r) => r.data),
};

/* ---------- Partneri ---------- */

/* ---------- Novosti (R1) ---------- */

export const novostiApi = {
  /** Prijava iz footera; `web` je honeypot. Ponovljena prijava je takođe uspjeh. */
  prijavi: (body: { email: string; web?: string }) =>
    apiFetch<{ prijavljen: boolean }>("/novosti/prijava", { method: "POST", body, auth: false }).then(
      (r) => r.data,
    ),
};

export const partneriApi = {
  posalji: (body: PartnerPrijavaInput) =>
    apiFetch<{ primljena: boolean }>("/partneri/prijave", { method: "POST", body, auth: false }).then(
      (r) => r.data,
    ),
};

/* ---------- Recepti ---------- */

export const receptiApi = {
  lista: () => apiFetch<ReceptKratko[]>("/recepti", { auth: false }).then((r) => r.data),
  recept: (slug: string) =>
    apiFetch<Recept>(`/recepti/${slug}`, { auth: false }).then((r) => r.data),
};

/* ---------- Admin: korpice ---------- */

export const adminKorpiceApi = {
  pregled: (godina: number, sedmica: number) =>
    apiFetch<SadrzajPregled[]>("/admin/korpice/sadrzaj", {
      query: { godina, sedmica },
    }).then((r) => r.data),

  /** Jedan sadržaj, uključujući nacrt — javni endpoint nacrt odbija. */
  sadrzaj: (id: string) =>
    apiFetch<AdminSadrzajKorpice>(`/admin/korpice/sadrzaj/${id}`).then((r) => r.data),

  generisi: (body: GenerisiSadrzajRequest) =>
    apiFetch<RezultatGenerisanja[]>("/admin/korpice/sadrzaj/generisi", {
      method: "POST",
      body,
    }).then((r) => r.data),

  izmijeni: (id: string, body: IzmijeniNacrtRequest) =>
    apiFetch<AdminSadrzajKorpice>(`/admin/korpice/sadrzaj/${id}`, {
      method: "PUT",
      body,
    }).then((r) => r.data),

  objavi: (id: string) =>
    apiFetch<AdminSadrzajKorpice>(`/admin/korpice/sadrzaj/${id}/objavi`, {
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

  ocijeni: (id: string, body: OcjenaInput) =>
    apiFetch<OcjenaIsporuke>(`/orders/${id}/ocjena`, { method: "POST", body }).then((r) => r.data),

  ocijeniPrekoTokena: (token: string, body: OcjenaInput) =>
    apiFetch<OcjenaIsporuke>(`/orders/track/${token}/ocjena`, {
      method: "POST",
      body,
      auth: false,
    }).then((r) => r.data),
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
  // `preporukaKod` (B5): upisuje se samo kad prijava pravi nov nalog.
  google: (idToken: string, preporukaKod?: string) =>
    apiFetch<AuthResult>("/auth/google", { method: "POST", body: { idToken, preporukaKod }, auth: false }).then(
      (r) => r.data,
    ),
  facebook: (accessToken: string, preporukaKod?: string) =>
    apiFetch<AuthResult>("/auth/facebook", {
      method: "POST",
      body: { accessToken, preporukaKod },
      auth: false,
    }).then((r) => r.data),
  me: () => apiFetch<AuthUser>("/auth/me").then((r) => r.data),
};

/* ---------- Nalog ---------- */

export const accountApi = {
  /** Pismo naloga — njime idu mejlovi i pretplatne narudžbe (P7). */
  postaviPismo: (pismo: Pismo) =>
    apiFetch<AuthUser>("/account/pismo", { method: "PUT", body: { pismo: pismoNaZici(pismo) } }).then((r) => r.data),

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

  /**
   * Jedan artikal korpice za drugi; količinu novog određuje server. `ubaci = null` je
   * „izaberi umjesto mene"; `zapamti` stavlja izbačeni artikal na spisak „ne stavljaj mi".
   */
  zamijeniUKorpici: (orderItemId: string, izbaci: string, ubaci: string | null, zapamti = false) =>
    apiFetch<MojaKorpicaDetalji>(`/account/korpice/${orderItemId}/zamijeni`, {
      method: "POST",
      body: { izbaci, ubaci, zapamti },
    }).then((r) => r.data),

  /** „Vrati kako je bilo": sadržaj sedmice, uz zamjene za spisak „ne stavljaj mi". */
  vratiKorpicu: (orderItemId: string) =>
    apiFetch<MojaKorpicaDetalji>(`/account/korpice/${orderItemId}/vrati`, { method: "POST" }).then((r) => r.data),

  izbjegnuti: () => apiFetch<IzbjegnutArtikal[]>("/account/izbjegnuti-artikli").then((r) => r.data),
  dodajIzbjegnut: (productId: string) =>
    apiFetch<IzbjegnutArtikal[]>(`/account/izbjegnuti-artikli/${productId}`, { method: "PUT" }).then((r) => r.data),
  ukloniIzbjegnut: (productId: string) =>
    apiFetch<IzbjegnutArtikal[]>(`/account/izbjegnuti-artikli/${productId}`, { method: "DELETE" }).then((r) => r.data),

  poeni: () => apiFetch<MojiPoeni>("/account/poeni").then((r) => r.data),

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
  zadaci: () => apiFetch<AdminZadaci>("/admin/zadaci").then((r) => r.data),

  /** Korpica jednog kupca u narudžbi — admin je uređuje sa količinama. */
  korpicaNarudzbe: (orderItemId: string) =>
    apiFetch<AdminKorpicaNarudzbe>(`/admin/narudzbe/korpica/${orderItemId}`).then((r) => r.data),
  izmijeniKorpicuNarudzbe: (orderItemId: string, stavke: { productId: string; kolicina: number }[]) =>
    apiFetch<AdminKorpicaNarudzbe>(`/admin/narudzbe/korpica/${orderItemId}`, {
      method: "PUT",
      body: { stavke },
    }).then((r) => r.data),

  dashboard: (date?: string) =>
    apiFetch<DashboardStats>("/admin/dashboard", { query: { date } }).then(
      (r) => r.data,
    ),

  /**
   * `unassigned` mijenja značenje `date`: tada je to dan DOSTAVE i vraćaju se samo narudžbe
   * koje tog dana nisu ni na čijoj ruti. Bez njega `date` je dan kreiranja narudžbe.
   */
  orders: (
    params: {
      status?: OrderStatus;
      date?: string;
      unassigned?: boolean;
      godina?: number;
      sedmica?: number;
      pretraga?: string;
    } = {},
  ) =>
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

  /* Slike (u bazi) — vraća URL za polje slike */
  uploadSlika: (fajl: File, folder: FolderSlike) => {
    const body = new FormData();
    body.append("fajl", fajl);
    body.append("folder", folder);
    return apiFetch<{ url: string; kljuc: string; velicina: number }>("/admin/slike", {
      method: "POST",
      body,
    }).then((r) => r.data);
  },

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

  ocjene: (ocjena?: number) =>
    apiFetch<OcjeneAdmin>(`/admin/ocjene${ocjena ? `?ocjena=${ocjena}` : ""}`).then((r) => r.data),

  napraviUtisak: (id: string) =>
    apiFetch<Utisak>(`/admin/ocjene/${id}/utisak`, { method: "POST" }).then((r) => r.data),

  /* Promo kodovi */
  promoKodovi: () => apiFetch<PromoKod[]>("/admin/promo-kodovi").then((r) => r.data),

  createPromoKod: (body: PromoKodInput) =>
    apiFetch<PromoKod>("/admin/promo-kodovi", { method: "POST", body }).then((r) => r.data),

  updatePromoKod: (id: string, body: PromoKodInput) =>
    apiFetch<PromoKod>(`/admin/promo-kodovi/${id}`, { method: "PUT", body }).then((r) => r.data),

  deletePromoKod: (id: string) =>
    apiFetch<string>(`/admin/promo-kodovi/${id}`, { method: "DELETE" }).then(() => id),

  deleteUtisak: (id: string) =>
    apiFetch<string>(`/admin/utisci/${id}`, { method: "DELETE" }).then(() => id),

  /* Partneri */
  novosti: () =>
    apiFetch<{ email: string; kreirana: string }[]>("/admin/novosti").then((r) => r.data),

  pretplate: (status?: string) =>
    apiFetch<AdminPretplate>("/admin/pretplate", { query: { status } }).then((r) => r.data),

  /** Otkaz od strane podrške — radi i dok traje obaveza plana (R2). */
  otkaziPretplatu: (id: string): Promise<void> =>
    apiFetch<void>(`/admin/pretplate/${id}`, { method: "DELETE" }).then(() => undefined),

  /* Pakovanje, zone i termini, kupci, sadržaj sajta */
  pakovanje: (godina: number, sedmica: number) =>
    apiFetch<Pakovanje>("/admin/pakovanje", { query: { godina, sedmica } }).then((r) => r.data),

  zone: () => apiFetch<DeliveryZone[]>("/admin/zone").then((r) => r.data),
  sacuvajZonu: (id: string | null, body: Omit<DeliveryZone, "id">) =>
    apiFetch<DeliveryZone>(id ? "/admin/zone/" + id : "/admin/zone", { method: id ? "PUT" : "POST", body }).then(
      (r) => r.data,
    ),

  termini: () => apiFetch<DeliverySlot[]>("/admin/termini").then((r) => r.data),
  sacuvajTermin: (id: string | null, body: Omit<DeliverySlot, "id">) =>
    apiFetch<DeliverySlot>(id ? "/admin/termini/" + id : "/admin/termini", {
      method: id ? "PUT" : "POST",
      body,
    }).then((r) => r.data),
  obrisiTermin: (id: string) =>
    apiFetch<string>("/admin/termini/" + id, { method: "DELETE" }).then(() => id),

  kupci: (pretraga?: string) =>
    apiFetch<AdminKupac[]>("/admin/kupci", { query: { pretraga: pretraga || undefined } }).then((r) => r.data),
  kupac: (id: string) => apiFetch<AdminKupacDetalj>("/admin/kupci/" + id).then((r) => r.data),

  sacuvajSadrzajSajta: (body: {
    telefon?: string | null;
    email?: string | null;
    adresa?: string | null;
    podrucje?: string | null;
    pitanja?: Pitanje[] | null;
  }) => apiFetch<SadrzajSajta>("/admin/sadrzaj-sajta", { method: "PUT", body }).then((r) => r.data),

  /* Poeni kupaca (B4) */
  poeniKupci: (pretraga?: string) =>
    apiFetch<AdminPoeniKupac[]>("/admin/poeni", { query: { pretraga: pretraga || undefined } }).then((r) => r.data),

  poeniKupca: (userId: string) =>
    apiFetch<AdminPoeniKupca>("/admin/poeni/" + userId).then((r) => r.data),

  korigujPoene: (userId: string, body: { iznos: number; razlog: string }) =>
    apiFetch<AdminPoeniKupca>("/admin/poeni/" + userId + "/korekcija", { method: "POST", body }).then((r) => r.data),

  partneri: (status?: StatusPrijave) =>
    apiFetch<PartnerPrijava[]>("/admin/partneri", { query: { status } }).then((r) => r.data),

  azurirajPrijavu: (id: string, body: PartnerStatusInput) =>
    apiFetch<PartnerPrijava>(`/admin/partneri/${id}`, { method: "PATCH", body }).then((r) => r.data),

  /* Analize */
  analize: () => apiFetch<Analiza[]>("/admin/analize").then((r) => r.data),

  createAnaliza: (body: AnalizaInput) =>
    apiFetch<Analiza>("/admin/analize", { method: "POST", body }).then((r) => r.data),

  updateAnaliza: (id: string, body: AnalizaInput) =>
    apiFetch<Analiza>(`/admin/analize/${id}`, { method: "PUT", body }).then((r) => r.data),

  deleteAnaliza: (id: string) =>
    apiFetch<string>(`/admin/analize/${id}`, { method: "DELETE" }).then(() => id),

  /* Recepti */
  recepti: () => apiFetch<ReceptAdmin[]>("/admin/recepti").then((r) => r.data),

  createRecept: (body: ReceptInput) =>
    apiFetch<ReceptAdmin>("/admin/recepti", { method: "POST", body }).then((r) => r.data),

  updateRecept: (id: string, body: ReceptInput) =>
    apiFetch<ReceptAdmin>(`/admin/recepti/${id}`, { method: "PUT", body }).then((r) => r.data),

  deleteRecept: (id: string) =>
    apiFetch<string>(`/admin/recepti/${id}`, { method: "DELETE" }).then(() => id),

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

/* ---------- Promo kodovi ---------- */

export const promoApi = {
  provjeri: (body: {
    kod: string;
    email?: string;
    items: { productId: string; quantity: number }[];
  }) =>
    apiFetch<ProvjeraPromoKoda>("/promo-kodovi/provjera", { method: "POST", body }).then(
      (r) => r.data,
    ),
};
