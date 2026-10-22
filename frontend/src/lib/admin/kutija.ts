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
 * Količina za artikal koji admin dodaje: popuni ono što fali do cilja (ali bar jedan korak);
 * kad je kutija već puna, jedan korak.
 */
export function kolicinaZaDodavanje(cilj: number, trenutno: number, cijena: number, jedinica: string): number {
  const fali = cilj - trenutno;
  return fali > 0 ? kolicinaZaVrijednost(fali, cijena, jedinica) : korak(jedinica);
}

/** Zamjena reda drugim artiklom iste vrijednosti — kao kupčeva zamjena na serveru. */
export function zamijeni(red: KutijaRed, nov: Product): KutijaRed {
  return {
    productId: nov.id,
    naziv: nov.name,
    jedinica: nov.unit,
    kolicina: kolicinaZaVrijednost(red.kolicina * red.cijena, nov.price, nov.unit),
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
