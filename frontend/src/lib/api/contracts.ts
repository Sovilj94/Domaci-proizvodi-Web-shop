import type {
  MojaKorpica,
  OrderStatus,
  PonudaArtikla,
  StatusSadrzaja,
  StavkaMojeKorpice,
  TezinaRecepta,
  TipKorpice,
  Unit,
  VelicinaKorpice,
} from "@/types/domain";

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

/** Tijelo za POST/PUT na `/account/addresses` — engleski ključevi (backend kontrakt). */
export interface AddressInput {
  label?: string;
  fullAddress: string;
  latitude?: number;
  longitude?: number;
  deliveryZoneId?: string;
}

/** Tijelo za PUT `/account/profile` — srpski ključevi, jer dolazi iz `AzurirajProfilCommand`. */
export interface ProfileInput {
  ime: string;
  telefon: string;
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
  /** Smije li ovaj artikal ući u sadržaj korpice (vidi Product.MozeUKorpicu). */
  mozeUKorpicu?: boolean;
  /** 0 = osnovni artikal, veći broj = dopuna (vidi Product.PrioritetUKorpici). */
  prioritetUKorpici?: number;
  /** Mjeseci (1–12) sezone; praznina/izostanak znači cijele godine. */
  mjeseci?: number[];
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

export interface UtisakInput {
  ime: string;
  mjesto?: string;
  tekst: string;
  objavljen: boolean;
  redoslijed: number;
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

/** Status admin pregleda — širi `StatusSadrzaja` sa "Nema" za korpicu bez sadržaja te sedmice. */
export type StatusPregleda = StatusSadrzaja | "Nema";

export interface SadrzajPregled {
  /** Odsutno na žici kad korpica te sedmice nema sadržaj (status "Nema"). */
  sadrzajId?: string;
  korpicaId: string;
  korpicaNaziv: string;
  tip: TipKorpice;
  velicina: VelicinaKorpice;
  status: StatusPregleda;
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
  /** Artikal je obrisan (ili trajno nestao) poslije objave sadržaja koji ga sadrži —
   * red i dalje nosi pravu količinu, ali ga treba posebno provjeriti pred nabavku. */
  artikalObrisan: boolean;
}

export interface PakerskaLista {
  brojNarudzbi: number;
  stavke: StavkaPakerske[];
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
  /** Odsutno na žici kad nema upozorenja — ne stiže kao `null`. */
  upozorenje?: string;
}

/**
 * Preslikava `MojaKorpicaDetaljiDto` — odgovor na `GET`/`PUT` `/account/korpice/{orderItemId}`.
 * Envelope specifičan za ekran izmjene jedne korpice (vidi `MojaKorpica`, `StavkaMojeKorpice`,
 * `PonudaArtikla` u `types/domain.ts`), zato ovdje i ne u domain.ts.
 */
export interface MojaKorpicaDetalji {
  korpica: MojaKorpica;
  stavke: StavkaMojeKorpice[];
  ponuda: PonudaArtikla[];
}

/**
 * Tijelo za `POST`/`PUT` `/account/pretplate`. Greške oko adrese i tačke backend vraća pod
 * ključem `fullAddress` (dijeli `AddressRules` sa adresarom), ne pod `deliveryAddress` —
 * forma zato grešku čita odatle.
 */
export interface PretplataInput {
  productId: string;
  deliveryZoneId: string;
  deliverySlotId: string;
  deliveryAddress: string;
  latitude?: number;
  longitude?: number;
  napomena?: string;
}

export interface SastojakInput {
  naziv: string;
  kolicina?: string;
  productId?: string;
}

/** Koraci idu kao jedan tekst, redovi odvojeni sa "\n". */
export interface ReceptInput {
  naslov: string;
  opis?: string;
  slikaUrl?: string;
  vrijemeMinuta: number;
  porcije: number;
  tezina: TezinaRecepta;
  koraci: string;
  objavljen: boolean;
  sastojci?: SastojakInput[];
}
