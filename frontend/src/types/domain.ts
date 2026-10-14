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
  /** Do četiri dostupna obična artikla, po imenu. Uvijek prisutno; [] kad nema. */
  artikli: string[];
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
  /** Smije li ovaj artikal ući u sadržaj korpice. */
  mozeUKorpicu?: boolean;
  /** 0 = osnovni artikal, veći broj = dopuna. */
  prioritetUKorpici?: number;
  /** Mjeseci (1–12) sezone; praznina znači cijele godine. */
  mjeseci?: number[];
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
  customerEmail?: string;
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
  totalAmount?: number;
  /** Kad je vozač zatvorio stanicu (ISO, UTC); null dok je otvorena. */
  zatvorenaU?: string | null;
}

export interface DeliveryRoute {
  id: string;
  date: string;
  driverId?: string;
  driverName?: string;
  status: RouteStatus;
  stops: RouteStop[];
  /** Naziv koji vidi vozač — naziv zone, uz redni broj kad zona ima više ruta ("Centar 1"). */
  naziv?: string;
  deliveryZoneId?: string | null;
  /** Rezultat optimizacije; null kad nije računata — 0 bi lagalo da je ruta duga 0 km. */
  duzinaMetara?: number | null;
  trajanjeSekundi?: number | null;
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
 * Preslikava `AddressDto` (backend `DTOs/AccountDtos.cs`) 1:1. Nullable backend
 * polja su na žici odsutna kad su null (`WhenWritingNull`), pa `?:`, nikad `| null`.
 */
export interface Address {
  id: string;
  label?: string;
  fullAddress: string;
  latitude?: number;
  longitude?: number;
  deliveryZoneId?: string;
  deliveryZoneName?: string;
  /** Zona se može ugasiti nakon što je adresa sačuvana — klijent to mora znati. */
  deliveryZoneActive: boolean;
  isDefault: boolean;
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

/**
 * Pogodak geokodiranja sa našeg `/geocode`. Pregledač Nominatim ne zove direktno —
 * vidi komentar u `components/shop/address-picker.tsx`.
 */
export interface GeocodeHit {
  latitude: number;
  longitude: number;
  displayName: string;
  localities: string[];
}

export const TIPOVI_KORPICE = [
  "povrce",
  "voce",
  "mlijecna",
  "suhomesnata",
  "domacinska",
] as const;
export type TipKorpice = (typeof TIPOVI_KORPICE)[number];

export type VelicinaKorpice = "mala" | "srednja" | "velika";

/**
 * Status stvarnog sadržaja — `SadrzajKorpice` postoji samo kad sadržaj postoji,
 * pa ovdje nema "Nema" (to je samo stanje admin pregleda; vidi `StatusPregleda`).
 */
export type StatusSadrzaja = "Nacrt" | "Objavljen" | "Zakljucan";

export interface Korpica {
  id: string;
  naziv: string;
  slug: string;
  tip: TipKorpice;
  velicina: VelicinaKorpice;
  cijena: number;
  /** Cijena poslije zarađenog popusta. Računa je server (`PretplatnaCijena`), ne klijent. */
  cijenaSaPretplatom?: number;
  /** Koliko isporuka po punoj cijeni prethodi popustu. */
  isporukaDoPopusta?: number;
  imageUrl?: string;
  opis: string;
  stock: number;
  dostupna: boolean;
}

export interface StavkaSadrzaja {
  productId: string;
  naziv: string;
  kolicina: number;
  jedinica: Unit;
  imageUrl?: string;
  proizvodjacNaziv: string;
  proizvodjacSelo: string;
}

export interface SadrzajKorpice {
  id: string;
  korpicaId: string;
  godina: number;
  sedmica: number;
  status: StatusSadrzaja;
  ciljnaVrijednost: number;
  stavke: StavkaSadrzaja[];
}

export interface Sedmica {
  godina: number;
  sedmica: number;
  /** ISO 8601, UTC. */
  datumIsporuke: string;
  rokNarucivanja: string;
  rokIstekao: boolean;
  /** Isporuka i rok NAREDNE sedmice (ISO 8601, UTC) — do njih broji odbrojavanje poslije roka. */
  sljedecaIsporuka: string;
  sljedeciRok: string;
}

/**
 * Preslikava `MojaKorpicaDto` (backend `DTOs/KorpiceDtos.cs`) 1:1 — red na
 * `GET /account/korpice`, jedna korpica koju je kupac naručio i koju još može
 * mijenjati prije roka.
 */
export interface MojaKorpica {
  orderItemId: string;
  orderId: string;
  kodNarudzbe: string;
  naziv: string;
  imageUrl?: string;
  godina: number;
  sedmica: number;
  /** ISO 8601 sa "Z" — backend ga šalje kao UTC. */
  rokIzmjene: string;
  ciljnaVrijednost: number;
  trenutnaVrijednost: number;
  brojArtikala: number;
  minimalanBrojArtikala: number;
}

/** Preslikava `StavkaMojeKorpiceDto` — stavka koju kupac trenutno ima u svojoj korpici. */
export interface StavkaMojeKorpice {
  productId: string;
  naziv: string;
  kolicina: number;
  jedinica: string;
  jedinicnaCijena: number;
  dodaoKupac: boolean;
}

/** Preslikava `PonudaArtiklaDto` — artikal koji kupac može dodati u svoju korpicu. */
export interface PonudaArtikla {
  productId: string;
  naziv: string;
  jedinica: string;
  cijena: number;
  imageUrl?: string;
  izOveKorpice: boolean;
}

/** `StatusPretplate` sa backenda — isti nazivi, jer DTO šalje `.ToString()`. */
export type StatusPretplate = "Aktivna" | "Pauzirana" | "Otkazana";

/**
 * Preslikava `PretplataDto` (backend `DTOs/AccountDtos.cs`) 1:1. Cijene su IZVEDENE na
 * serveru pri svakom čitanju (vidi `PretplatnaCijena`) — klijent ih prikazuje, ne računa.
 */
export interface Pretplata {
  id: string;
  status: StatusPretplate;

  productId: string;
  naziv: string;
  slug: string;
  tip?: TipKorpice;
  velicina?: string;
  imageUrl?: string;

  deliveryAddress: string;
  latitude?: number;
  longitude?: number;
  deliveryZoneId: string;
  deliveryZoneName?: string;
  /** Zona se može ugasiti poslije pretplate — isto kao kod sačuvane adrese. */
  deliveryZoneActive: boolean;
  deliverySlotId: string;
  deliverySlotLabel?: string;
  napomena?: string;

  brojIsporuka: number;
  punaCijena: number;
  /** Cijena naredne isporuke; jednaka punoj dok popust nije zarađen. */
  trenutnaCijena: number;
  popustAktivan: boolean;
  /** Koliko još isporuka do niže cijene; 0 kad je kupac već ima. */
  doPopusta: number;

  sljedecaGodina?: number;
  sljedecaSedmica?: number;
  sljedecaIsporuka?: string;

  pauzaDoGodina?: number;
  pauzaDoSedmica?: number;
  createdAt: string;
}

/** Preslikava `UtisakDto` (backend `DTOs/UtisciDtos.cs`). `mjesto` je "" kad nije uneseno. */
export interface Utisak {
  id: string;
  ime: string;
  mjesto: string;
  tekst: string;
  objavljen: boolean;
  redoslijed: number;
}
