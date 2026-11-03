import type { StavkaPakerske } from "@/lib/api/contracts";

/** Jedan red spiska za nabavku: koliko čega treba, bez obzira od koga se kupuje. */
export interface RedNabavke {
  artikal: string;
  jedinica: string;
  kolicina: number;
  artikalObrisan: boolean;
  /** Nabavna vrijednost reda; null kad nijedan dio nema nabavnu cijenu. */
  trosak: number | null;
}

const zaokruzi = (x: number, decimala = 3) => Math.round(x * 10 ** decimala) / 10 ** decimala;

/**
 * Spisak robe za nabavku: isti artikal (naziv + jedinica) u jednom redu, ma od kog
 * proizvođača dolazio — nabavljača zanima šta i koliko, ne gazdinstvo. Redovi po nazivu.
 */
export function spisakRobe(stavke: StavkaPakerske[]): RedNabavke[] {
  const redovi = new Map<string, RedNabavke>();
  for (const s of stavke) {
    const kljuc = `${s.artikal.toLocaleLowerCase("sr-Latn")}|${s.jedinica}`;
    const r = redovi.get(kljuc) ?? {
      artikal: s.artikal,
      jedinica: s.jedinica,
      kolicina: 0,
      artikalObrisan: false,
      trosak: null,
    };
    r.kolicina = zaokruzi(r.kolicina + s.kolicina);
    r.artikalObrisan ||= s.artikalObrisan;
    if (s.nabavnaCijena && s.nabavnaCijena > 0) r.trosak = zaokruzi((r.trosak ?? 0) + s.kolicina * s.nabavnaCijena, 2);
    redovi.set(kljuc, r);
  }
  return [...redovi.values()].sort((a, b) => a.artikal.localeCompare(b.artikal, "sr-Latn"));
}
