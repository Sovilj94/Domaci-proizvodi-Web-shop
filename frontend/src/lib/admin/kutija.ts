import type { Product } from "@/types/domain";

/** Jedan red u kutiji koju admin uređuje (sedmični sadržaj ili korpica kupca). */
export interface KutijaRed {
  productId: string;
  naziv: string;
  jedinica: string;
  kolicina: number;
  /** Jedinična prodajna cijena po kojoj se red računa. */
  cijena: number;
  nabavna: number | null;
  imageUrl?: string | null;
  boja?: string | null;
  /** Nije iz sedmičnog sadržaja (korpica kupca). */
  izmijenjeno?: boolean;
}

/** Korak količine — isto pravilo kao generator na backendu: komad je cio, kg i l na 0,1. */
export function korak(jedinica: string): number {
  return jedinica === "kom" ? 1 : 0.1;
}

/** Količina zaokružena na korak jedinice, bez repova binarnog zapisa (0.30000000000000004). */
export function naKorak(kolicina: number, jedinica: string): number {
  const k = korak(jedinica);
  return Math.round(Math.round(kolicina / k) * k * 10) / 10;
}

/** Koliko artikla daje traženu vrijednost — najmanje jedan korak. */
export function kolicinaZaVrijednost(vrijednost: number, cijena: number, jedinica: string): number {
  if (cijena <= 0) return korak(jedinica);
  return Math.max(korak(jedinica), naKorak(vrijednost / cijena, jedinica));
}

export interface Zbir {
  vrijednost: number;
  nabavno: number;
  /** Redova bez nabavne cijene — njihov trošak nije uračunat. */
  bezNabavne: number;
  /** Marža prema cijeni korpice; null kad nijedan red nema nabavnu ili cijena nije poznata. */
  marza: number | null;
}

export function zbir(redovi: KutijaRed[], cijenaKorpice: number): Zbir {
  const vrijednost = Math.round(redovi.reduce((z, r) => z + r.kolicina * r.cijena, 0) * 100) / 100;
  const nabavno = Math.round(redovi.reduce((z, r) => z + r.kolicina * (r.nabavna ?? 0), 0) * 100) / 100;
  const bezNabavne = redovi.filter((r) => r.nabavna == null).length;
  const marza =
    cijenaKorpice > 0 && redovi.length > bezNabavne ? Math.round(((cijenaKorpice - nabavno) / cijenaKorpice) * 10000) / 10000 : null;
  return { vrijednost, nabavno, bezNabavne, marza };
}

/**
 * Koliko artikla stane u `slobodno` KM, zaokruženo naniže (0 kad ne stane ni jedan korak —
 * prvo treba nešto izvaditi). Bez granice (cilj nepoznat): jedan korak.
 */
export function kolicinaZaDodavanje(slobodno: number, cijena: number, jedinica: string): number {
  if (!Number.isFinite(slobodno) || cijena <= 0) return korak(jedinica);
  return naKorakDolje((slobodno + 0.005) / cijena, jedinica);
}

/** Količina zaokružena NANIŽE na korak jedinice — da se granica nikad ne probije zaokruživanjem. */
export function naKorakDolje(kolicina: number, jedinica: string): number {
  const k = korak(jedinica);
  return Math.round(Math.floor(kolicina / k + 1e-9) * k * 10) / 10;
}

/**
 * Najviše koliko kutija smije da vrijedi: cilj, ili zatečena vrijednost ako je već bila preko
 * (tada smije samo da se smanjuje). `null` kad cilj nije poznat — tada nema granice.
 * Isto pravilo kao server (GranicaKutije.cs).
 */
export function granicaKutije(cilj: number, pocetnaVrijednost: number): number | null {
  return cilj > 0 ? Math.max(cilj, pocetnaVrijednost) : null;
}

/** Koliko KM se još smije dodati: ono što je oslobođeno vađenjem ili smanjivanjem. */
export function slobodnoZaDodavanje(granica: number | null, vrijednost: number): number {
  return granica === null ? Infinity : Math.max(0, Math.round((granica - vrijednost) * 100) / 100);
}

/** Najveća količina reda da kutija ne pređe granicu — nikad manja od one koja već stoji. */
export function najvecaKolicina(red: KutijaRed, slobodno: number): number {
  if (!Number.isFinite(slobodno) || red.cijena <= 0) return Infinity;
  return Math.max(red.kolicina, naKorakDolje(red.kolicina + (slobodno + 0.005) / red.cijena, red.jedinica));
}

/**
 * Zamjena reda drugim artiklom — po vrijednosti: koliko KM izađe, toliko KM novog artikla uđe
 * (2 kg krompira × 2 KM = 4 KM → batat po 5 KM/kg = 0,8 kg), kao kupčeva zamjena na serveru.
 * Zadate količine iz postavki su za osnovni sadržaj korpice, ne za zamjene. Količina se
 * zaokruži naniže kad bi naviše probila granicu (`slobodno` = KM slobodno u kutiji); `null`
 * kad ne stane ni jedan korak.
 */
export function zamijeni(red: KutijaRed, nov: Product, slobodno = Infinity): KutijaRed | null {
  const vrijednostReda = red.kolicina * red.cijena;
  const staje = (k: number) => !Number.isFinite(slobodno) || k * nov.price <= vrijednostReda + slobodno + 0.005;
  let kolicina = kolicinaZaVrijednost(vrijednostReda, nov.price, nov.unit);
  if (nov.price > 0 && !staje(kolicina)) {
    kolicina = naKorakDolje((vrijednostReda + slobodno + 0.005) / nov.price, nov.unit);
    if (kolicina <= 0) return null;
  }
  return {
    productId: nov.id,
    naziv: nov.name,
    jedinica: nov.unit,
    kolicina,
    cijena: nov.price,
    nabavna: nov.nabavnaCijena ?? null,
    imageUrl: nov.imageUrl,
    boja: nov.boja,
    izmijenjeno: true,
  };
}

/** Ono što smije u korpicu u datom mjesecu: obična roba, dostupna, sa zalihom i u sezoni. */
export function sezonskaPonuda(proizvodi: Product[], mjesec: number): Product[] {
  return proizvodi
    .filter(
      (p) =>
        p.isAvailable &&
        p.mozeUKorpicu &&
        p.stock > 0 &&
        (!p.mjeseci || p.mjeseci.length === 0 || p.mjeseci.includes(mjesec)),
    )
    .sort((a, b) => (a.prioritetUKorpici ?? 0) - (b.prioritetUKorpici ?? 0) || a.name.localeCompare(b.name, "sr"));
}

/** Mjesec u koji pada četvrtak ISO sedmice — isto pravilo kao generator (MjesecSedmice). */
export function mjesecSedmice(godina: number, sedmica: number): number {
  const jan4 = new Date(Date.UTC(godina, 0, 4));
  const ponedjeljak = new Date(jan4);
  ponedjeljak.setUTCDate(jan4.getUTCDate() - ((jan4.getUTCDay() + 6) % 7) + (sedmica - 1) * 7);
  ponedjeljak.setUTCDate(ponedjeljak.getUTCDate() + 3);
  return ponedjeljak.getUTCMonth() + 1;
}
