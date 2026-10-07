import type { OrderStatus, StatusSadrzaja, TipKorpice, Unit, VelicinaKorpice } from "@/types/domain";

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
  latitude?: number;
  longitude?: number;
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
  /** Neaktivan vozač nema tab na dispečerskom ekranu i ne prima nove narudžbe. */
  aktivan: boolean;
  /** Dnevni kapacitet u stanicama; dolazi sa servera da ga front ne bi zakucao kod sebe. */
  maxStanica: number;
  /** Vozilo kojim vozač obično dostavlja, npr. „Kombi 1". */
  vozilo?: string | null;
  /** Registarska oznaka vozila. */
  registracija?: string | null;
}

/** Tijelo za unos i izmjenu vozača. Nalog za mobilnu aplikaciju se ne dira odavde. */
export interface DriverInput {
  name: string;
  phone: string;
  vozilo?: string | null;
  registracija?: string | null;
  aktivan: boolean;
}

/** Tijelo za dodjelu/prebacivanje vozača; `date` je ISO "yyyy-MM-dd" — dan rute. */
export interface AssignDriverRequest {
  driverId: string;
  date: string;
}

/** Grupna dodjela — cijelo naselje jednom vozaču. Sve ili ništa: ako grupa ne stane u
 *  vozačev preostali kapacitet, server odbija cijelu dodjelu sa `driver_full`. */
export interface AssignOrdersBulkRequest {
  orderIds: string[];
  driverId: string;
  date: string;
}

/** Odgovor na poslatu narudžbu gosta — narudžba još ne postoji, samo zahtjev sa rokom. */
export interface GuestOrderRequestResult {
  expiresAt: string;
}

export interface SadrzajPregled {
  sadrzajId: string | null;
  korpicaId: string;
  korpicaNaziv: string;
  tip: TipKorpice;
  velicina: VelicinaKorpice;
  status: StatusSadrzaja;
  brojArtikala: number;
  vrijednost: number;
  ciljGenerisanja: number;
}

export interface StavkaPakerske {
  artikal: string;
  kolicina: number;
  jedinica: string;
  proizvodjac: string;
  selo: string;
}

export interface GenerisiSadrzajRequest {
  godina: number;
  sedmica: number;
  korpicaId?: string;
}

export interface IzmijeniNacrtRequest {
  stavke: { productId: string; kolicina: number }[];
}

export interface RezultatGenerisanja {
  sadrzajId: string;
  korpicaId: string;
  korpicaNaziv: string;
  brojArtikala: number;
  vrijednost: number;
  ciljGenerisanja: number;
  upozorenje: string | null;
}
