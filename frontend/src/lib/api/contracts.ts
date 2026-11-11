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
  /** Server ga ignoriše — naselje određuje iz tačke. */
  deliveryZoneId?: string;
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
  /** Ambalaža po jedinici prodaje (KM); izostanak = ne mijenjaj. */
  ambalazaCijena?: number;
  /** true = obriši unesenu ambalažu. */
  ukloniAmbalazu?: boolean;
  /** Zadate količine po veličini korpice; poslato = zamijeni sve tri (null = nije zadato), izostanak = ne mijenjaj. */
  kolicine?: { mala: number | null; srednja: number | null; velika: number | null };
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
  kategorija: string;
  /** Grupa nabavke: kategorija koja puni neku korpicu, inače „Špajz". */
  grupa: string;
  /** Redoslijed grupe (po tipu korpice); Špajz posljednji. */
  redGrupe: number;
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
  /** Potvrđene narudžbe u listi — samo za njih se nabavlja. */
  brojNarudzbi: number;
  /** Narudžbe koje čekaju potvrdu — nisu u listi dok se ne potvrde. */
  naCekanju: number;
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
  /** Server ga ignoriše — naselje određuje iz tačke. */
  deliveryZoneId?: string;
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

/* ---------- Poslovanje (admin analitika) ---------- */

export interface PrihodPoslovanja {
  prometRobe: number;
  dostava: number;
  promoPopust: number;
  poeniPopust: number;
  neto: number;
  nabavno: number;
  zarada: number;
  marza?: number | null;
  brojNarudzbi: number;
  prosjecnaNarudzba: number;
  udioKorpica: number;
  udioSpajza: number;
  procjena: boolean;
  bezNabavneCijene: number;
}

export interface KorpicaProdaje { naziv: string; tip: string; velicina: string; komada: number; prihod: number; nabavno: number; marza?: number | null }
export interface KategorijaProdaje { kategorija: string; prihod: number; nabavno: number; marza?: number | null }
export interface ArtikalProdaje { naziv: string; jedinica: string; kolicina: number; prihod: number; zarada: number }
export interface KupacAnalitike { ime: string; gost: boolean; narudzbi: number; neto: number }
export interface PlanAnalitike { plan: string; aktivne: number }
export interface ZonaAnalitike { zona: string; narudzbi: number; neto: number }
export interface ArtikalGazdinstva { naziv: string; jedinica: string; kolicina: number; nabavno: number }
export interface GazdinstvoAnalitike { naziv: string; selo: string; nabavno: number; artikli: ArtikalGazdinstva[] }

export interface PoslovanjeMjeseca {
  godina: number;
  mjesec: number;
  prihod: PrihodPoslovanja;
  prethodni: PrihodPoslovanja;
  prodaja: {
    korpice: KorpicaProdaje[];
    spajz: KategorijaProdaje[];
    topArtikli: ArtikalProdaje[];
    najslabijiArtikli: ArtikalProdaje[];
  };
  kupci: {
    aktivni: number;
    aktivniPrethodni: number;
    novi: number;
    povratni: number;
    zadrzavanje?: number | null;
    potrosnjaPoKupcu: number;
    narudzbiPoKupcu: number;
    gosti: number;
    registrovani: number;
    noviPrekoPreporuke: number;
    topKupci: KupacAnalitike[];
  };
  pretplate: {
    aktivneNaKraju: number;
    nove: number;
    otkazane: number;
    stopaOtkazivanja?: number | null;
    pauzirane?: number | null;
    poPlanu: PlanAnalitike[];
    udioPrihoda: number;
  };
  dostava: {
    poZoni: ZonaAnalitike[];
    isporucene: number;
    neuspjele: number;
    uspjesnost?: number | null;
    otkazane: number;
    otkazaneVrijednost: number;
    otkazaneUdio?: number | null;
    prosjecnaOcjena?: number | null;
    brojOcjena: number;
  };
  gazdinstva: GazdinstvoAnalitike[];
  troskovi: TroskoviPoslovanja;
  prethodniTroskovi: TroskoviPoslovanja;
}

export interface VrstaTroskaMjeseca { vrsta: string; jeGorivo: boolean; jednokratni: number; ponavljajuci: number; iznos: number }

export interface TroskoviPoslovanja {
  ambalaza: number;
  ambalazaProcjena: boolean;
  gorivo: {
    km: number;
    brojRuta: number;
    /** Vozač-dani: jedna dnevna tura sa povratkom u magacin. */
    brojDana: number;
    /** Po danima: plaćeno na pumpi gdje je uneseno, inače procjena. Nema je kad nijedan dan nema ni unos ni cijenu goriva. */
    poRutama?: number | null;
    /** Čista procjena svih dana, za poređenje sa plaćenim. */
    procijenjeno: number;
    placenoVozaci: number;
    danaSaUnosom: number;
    poRacunima: number;
    izvor: "racuni" | "rute";
    uDobiti: number;
    bezCijene: boolean;
  };
  poVrsti: VrstaTroskaMjeseca[];
  ostali: number;
  ukupno: number;
  dobit: number;
  marzaDobiti?: number | null;
  varijabilniPoNarudzbi: number;
}

/* ---------- Plan poslovanja ---------- */

export interface PlanNarudzba {
  korpice: number;
  nabavnoKorpica: number;
  kutijeKorpica: number;
  spajz: number;
  nabavnoSpajz: number;
  ambalazaSpajz: number;
  dostava: number;
  popusti: number;
  gorivo: number;
  neto: number;
  varijabilno: number;
  doprinos: number;
}

export interface PlanScenarij {
  narudzbi: number;
  neto: number;
  nabavno: number;
  ambalaza: number;
  gorivo: number;
  varijabilno: number;
  fiksni: number;
  dobit: number;
  marzaDobiti?: number | null;
}

export interface PlanPoslovanja {
  narudzbi: number;
  godina: number;
  mjesec: number;
  prosjek: PlanNarudzba;
  plan: PlanScenarij;
  scenariji: PlanScenarij[];
  prag?: number | null;
  fiksni: number;
  fiksniStavke: { vrsta: string; iznos: number; ponavljajuci: boolean }[];
  korpice: { naziv: string; naNarudzbu: number; cijena: number; nabavno: number; kutija: number; zarada: number }[];
  osnova: { godina: number; mjesec: number; narudzbi: number; neto: number; dobit: number }[];
  narudzbiOsnova: number;
  kapacitetSedmicno: number;
  narudzbiSedmicno: number;
}

/* ---------- Mejlovi: red, obavještenja, ručno slanje ---------- */

export type StatusEmaila = "NaCekanju" | "Poslato" | "Greska";

export interface EmailRed {
  id: string;
  primalac: string;
  primalacIme?: string | null;
  naslov: string;
  vrsta: string;
  status: StatusEmaila;
  pokusaja: number;
  zadnjaGreska?: string | null;
  kreirana: string;
  poslata?: string | null;
  sljedeciPokusaj?: string | null;
  transport?: string | null;
}

export interface EmailDetalj extends EmailRed {
  html: string;
  dobavljacId?: string | null;
}

export interface EmailPregled {
  poruke: EmailRed[];
  naCekanju: number;
  greske: number;
  poslatoDanas: number;
  poslatoMjesec: number;
  /** "smtp" (lokalni Mailpit ili SMTP server) ili "brevo". */
  transport: string;
  vrste: string[];
}

export interface ObavjestenjaPodesavanja {
  kupacPotvrdjena: boolean;
  kupacIsporucena: boolean;
  kupacNeuspjela: boolean;
  kupacOtkazana: boolean;
  radnikNovaNarudzba: boolean;
  radnikOdbijene: boolean;
  radnikFiskalnaGreska: boolean;
  radnikGorivo: boolean;
  vozacPlan: boolean;
  vozacPlanSat: number;
  radniciPrimaoci: string;
  /** Kome obavještenja radnicima stvarno idu (lista ili, kad je prazna, admini). */
  radniciEfektivno: string[];
}

export type GrupaKampanje = "sedmica" | "pretplatnici" | "kupci" | "novosti" | "vozaci" | "radnici";

export interface KampanjaPregled {
  opis: string;
  broj: number;
  primjeri: string[];
  saOdjavom: boolean;
}

export interface Kampanja {
  id: string;
  naslov: string;
  grupaOpis: string;
  brojPrimalaca: number;
  kreirana: string;
  poslato: number;
  greske: number;
}

export interface KampanjaInput {
  grupa: GrupaKampanje;
  godina?: number | null;
  sedmica?: number | null;
  naslov: string;
  tekst: string;
}

/* ---------- Dnevne vožnje (procjena ture i gorivo sa pumpe) ---------- */

export interface Voznja {
  id: string;
  driverId: string;
  vozac: string;
  datum: string;
  stanica: number;
  bezKoordinata: number;
  /** Dnevna tura magacin → sve stanice → magacin; nema je kad nijedna stanica nema koordinate. */
  km?: number | null;
  trajanjeMin?: number | null;
  izvor?: "osrm" | "vazdusno" | null;
  cijenaPoLitru?: number | null;
  potrosnjaL100km?: number | null;
  procjenaLitara?: number | null;
  procjenaGoriva?: number | null;
  placenoGorivo?: number | null;
  litara?: number | null;
  napomena?: string | null;
  placenoUnio?: "vozac" | "admin" | null;
  placenoUneseno?: string | null;
  odstupanjeProcenat?: number | null;
  bitnoOdstupanje: boolean;
}

export interface GorivoUnos {
  /** null briše unos — dan se opet računa po procjeni. */
  iznos: number | null;
  litara: number | null;
  napomena: string | null;
}

/* ---------- Fiskalizacija ---------- */

export type StatusFiskalnog = "NaCekanju" | "Izdat" | "Greska";
export interface FiskalniRacunRed {
  id: string;
  orderId: string;
  kodNarudzbe: string;
  kupac: string;
  vrsta: "Promet" | "Refundacija";
  status: StatusFiskalnog;
  iznos: number;
  kreiran: string;
  izdat?: string | null;
  broj?: string | null;
  pokusaja: number;
  zadnjaGreska?: string | null;
  trajnaGreska: boolean;
  sljedeciPokusaj?: string | null;
  refundiran: boolean;
}
export interface FiskalnaStavka {
  naziv: string; kolicina: number; jedinica: string; jedinicnaCijena: number; popust: number; ukupno: number; poreskaOznaka: string;
}
export interface FiskalniRacun extends FiskalniRacunRed {
  nacinPlacanja: string;
  dobavljac?: string | null;
  brojac?: string | null;
  qrSadrzaj?: string | null;
  verifikacijaUrl?: string | null;
  refundiraniBroj?: string | null;
  stavke: FiskalnaStavka[];
}
export interface FiskalniPregled {
  od: string;
  do: string;
  racuni: FiskalniRacunRed[];
  izdato: number;
  iznosPromet: number;
  iznosRefundacija: number;
  naCekanju: number;
  greske: number;
  dobavljac: string;
  probni: boolean;
}
export interface FiskalnaPodesavanja {
  pdvObveznik: boolean;
  stopaPdv: number;
  oznakaPdv: string;
  oznakaBezPdv: string;
  dobavljac: string;
  probni: boolean;
}

/* ---------- Troškovi (admin unos) ---------- */

export interface Trosak { id: string; datum: string; vrstaId: string; vrsta: string; jeGorivo: boolean; iznos: number; opis: string; racunUrl?: string | null }
export interface PonavljajuciTrosak {
  id: string; vrstaId: string; vrsta: string; iznos: number; opis: string;
  odGodina: number; odMjesec: number; doGodina?: number | null; doMjesec?: number | null; vaziUMjesecu: boolean;
}
export interface VrstaTroska { id: string; naziv: string; jeGorivo: boolean; redoslijed: number; aktivna: boolean; uUpotrebi: boolean }
export interface CijenaGoriva { id: string; vaziOd: string; cijenaPoLitru: number; potrosnjaL100km: number }
export interface TroskoviMjeseca {
  godina: number;
  mjesec: number;
  troskovi: Trosak[];
  ponavljajuci: PonavljajuciTrosak[];
  vrste: VrstaTroska[];
  cijeneGoriva: CijenaGoriva[];
}
export interface TrosakInput { datum: string; vrstaId: string; iznos: number; opis?: string; racunUrl?: string | null }
export interface VrstaTroskaInput { naziv: string; jeGorivo: boolean; redoslijed: number; aktivna: boolean }

export interface MjesecGodine {
  mjesec: number;
  neto: number;
  zarada: number;
  marza?: number | null;
  narudzbi: number;
  aktivniKupci: number;
  /** Nema je za mjesec koji još nije počeo. */
  aktivnePretplate?: number | null;
  procjena: boolean;
  troskovi: number;
  dobit: number;
}

export interface PoslovanjeGodine {
  godina: number;
  mjeseci: MjesecGodine[];
  sezona: { kategorija: string; mjeseci: number[] }[];
}
