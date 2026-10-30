import type {
  MojaKorpica,
  OrderStatus,
  PonudaArtikla,
  RezultatAnalize,
  StatusPrijave,
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

export interface OcjenaInput {
  ocjena: number;
  komentar?: string;
  dozvolaZaObjavu: boolean;
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
  promoKod?: string;
  iskoristiPoene?: boolean;
}

export interface RegisterRequest {
  name: string;
  email: string;
  phone: string;
  password: string;
  address?: string;
  deliveryZoneId?: string;
  preporukaKod?: string;
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
  /** Boja pločice „#RRGGBB"; "" = boja iz palete, izostanak = ne mijenjaj (R1). */
  boja?: string;
  /** Nabavna cijena po jedinici; izostanak = ne mijenjaj. */
  nabavnaCijena?: number;
  /** true = obriši unesenu nabavnu cijenu. */
  ukloniNabavnuCijenu?: boolean;
  /** Slika bez pozadine za pločice korpica (`/slike/izrezi/...`). */
  izrezUrl?: string;
  /** Naziv i opis na engleskom (jezik sajta English); prazno = srpski. */
  nazivEn?: string;
  opisEn?: string;
  /** Prodaje se pojedinačno u Špajzu; false = samo u korpicama. */
  uSpajzu?: boolean;
}

export interface CategoryInput {
  name: string;
  imageUrl?: string;
  sortOrder: number;
  nazivEn?: string;
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

/** Zadatak sa početnog ekrana admina (`GET /admin/zadaci`). */
export interface AdminZadatak {
  kljuc: string;
  nivo: "hitno" | "upozorenje" | "info";
  naslov: string;
  opis: string;
  broj: number;
  link: string;
  akcija: string;
}

export interface AdminZadaci {
  godina: number;
  sedmica: number;
  zadaci: AdminZadatak[];
  /** Brojke uz stavke menija: narudzbe, korpice, proizvodi, partneri. */
  meni: Record<string, number>;
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
  cijenaKorpice: number;
  nabavnaVrijednost: number;
  /** Odsutno kad nijedna stavka nema nabavnu cijenu. */
  marza?: number | null;
  bezNabavneCijene: number;
  brojZamjena: number;
}

export interface StavkaPakerske {
  artikal: string;
  /** Ukupno potrebno: sadržaj korpica + Špajz. */
  kolicina: number;
  uKorpicama: number;
  uSpajzu: number;
  /** Trenutno stanje, već umanjeno za prodato iz Špajza. */
  naStanju: number;
  /** Koliko korpice traže preko stanja — to treba nabaviti. */
  fali: number;
  jedinica: string;
  proizvodjac: string;
  selo: string;
  telefon?: string | null;
  /** Artikal je obrisan (ili trajno nestao) poslije objave sadržaja koji ga sadrži —
   * red i dalje nosi pravu količinu, ali ga treba posebno provjeriti pred nabavku. */
  artikalObrisan: boolean;
  nabavnaCijena?: number | null;
}

export interface PakerskaLista {
  godina: number;
  sedmica: number;
  brojNarudzbi: number;
  /** Rok naručivanja te sedmice (ISO, UTC). */
  rok: string;
  /** Prvi dan isporuke te sedmice (ISO, UTC) — do tada roba mora biti nabavljena. */
  isporuka: string;
  /** Rok je prošao — količine su konačne. */
  zakljucena: boolean;
  /** Trošak nabavke liste po trenutnim nabavnim cijenama (artikli bez nabavne se ne broje). */
  nabavnaVrijednost: number;
  bezNabavneCijene: number;
  stavke: StavkaPakerske[];
}

/** Korpice na čekanju za jednu sedmicu i koliko ih stanje dozvoljava da se potvrdi. */
export interface PotvrdjivanjeSedmice {
  godina: number;
  sedmica: number;
  korpice: {
    korpicaId: string;
    naziv: string;
    naCekanju: number;
    /** Koliko ih stanje pokriva kad se potvrđuju redom kojim su stigle. */
    mozeSePotvrditi: number;
    /** Artikal kojeg je prvog nestalo, kad ne mogu sve. */
    nedostaje?: string | null;
  }[];
}

export interface GenerisiSadrzajRequest {
  godina: number;
  sedmica: number;
  korpicaId?: string;
  /** true = postojeći NACRT se briše i sastavlja iznova (objavljen se ne dira). */
  ponovo?: boolean;
}

export interface IzmijeniNacrtRequest {
  stavke: { productId: string; kolicina: number }[];
  /** Spisak predloženih zamjena; izostanak = ne mijenjaj. */
  zamjene?: string[];
}

export interface RezultatGenerisanja {
  sadrzajId: string;
  korpicaId: string;
  korpicaNaziv: string;
  brojArtikala: number;
  vrijednost: number;
  ciljGenerisanja: number;
  nabavnaVrijednost: number;
  marza?: number | null;
  /** Artikli u sadržaju bez nabavne cijene — za njih marža nije provjerena. */
  bezNabavneCijene: string[];
  brojZamjena: number;
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
  /** Kupčev spisak „ne stavljaj mi" (productId) — birač zamjene ih ne nudi. */
  izbjegnuti: string[];
}

/** Artikal sa kupčevog spiska „ne stavljaj mi". */
export interface IzbjegnutArtikal {
  productId: string;
  naziv: string;
  imageUrl?: string | null;
}

/** Korpica jednog kupca u narudžbi, kako je admin uređuje (`/admin/narudzbe/korpica/{id}`). */
export interface AdminKorpicaNarudzbe {
  orderItemId: string;
  kodNarudzbe: string;
  statusNarudzbe: string;
  kupac: string;
  korpica: string;
  godina: number;
  sedmica: number;
  brojKutija: number;
  cijenaKorpice: number;
  cilj: number;
  vrijednost: number;
  nabavnaVrijednost: number;
  marza: number | null;
  mozeSeMijenjati: boolean;
  stavke: {
    productId: string;
    naziv: string;
    jedinica: string;
    kolicina: number;
    jedinicnaCijena: number;
    nabavnaCijena: number | null;
    izmijenjeno: boolean;
  }[];
  izbjegnuti: IzbjegnutArtikal[];
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
  /** Samo pri novoj pretplati (R2); izostanak = Nedjeljna. */
  plan?: import("@/types/domain").PlanPretplate;
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

export interface AnalizaInput {
  producerId: string;
  productId?: string;
  uzorak: string;
  laboratorija: string;
  brojIzvjestaja?: string;
  /** "yyyy-MM-dd" — vrijednost `<input type="date">` šalje se neizmijenjena. */
  datumUzorkovanja: string;
  ispitano: string;
  rezultat: RezultatAnalize;
  napomena?: string;
  pdfUrl?: string;
  objavljena: boolean;
}

/** `web` je honeypot: ljudi ga ne vide, pa ostaje prazan. */
export interface PartnerPrijavaInput {
  ime: string;
  gazdinstvo?: string;
  mjesto: string;
  telefon: string;
  email?: string;
  proizvodi: string;
  web?: string;
}

/** Izostavljena `adminNapomena` ostavlja staru, "" je briše. */
export interface PartnerStatusInput {
  status: StatusPrijave;
  adminNapomena?: string;
}

export interface ProvjeraPromoKoda {
  kod: string;
  opis: string;
  popust: number;
  subtotal: number;
}

export interface PromoKodInput {
  kod: string;
  opis: string;
  tip: "Procenat" | "Iznos";
  vrijednost: number;
  minIznos: number | null;
  vaziOd: string | null;
  vaziDo: string | null;
  maxKoristenja: number | null;
  jednomPoKupcu: boolean;
  samoNoviKupci: boolean;
  aktivan: boolean;
}
