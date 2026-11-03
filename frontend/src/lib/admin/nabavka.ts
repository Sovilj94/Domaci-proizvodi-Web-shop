import type { StavkaPakerske } from "@/lib/api/contracts";

/** Jedan red spiska za nabavku: koliko čega treba, bez obzira od koga se kupuje. */
export interface RedNabavke {
  artikal: string;
  jedinica: string;
  kolicina: number;
  artikalObrisan: boolean;
  /** Nabavna vrijednost reda; null kad nijedan dio nema nabavnu cijenu. */
  trosak: number | null;
  kategorija: string;
  /** Grupa nabavke (Povrće, Voće, … ili Špajz) — dolazi sa servera, vidi StavkaPakerske.grupa. */
  grupa: string;
  redGrupe: number;
}

/** Grupa spiska: artikli jedne vrste robe, uz zbir nabavne vrijednosti. */
export interface GrupaNabavke {
  naziv: string;
  redovi: RedNabavke[];
  /** Zbir nabavne vrijednosti grupe; null kad nijedan red nema nabavnu cijenu. */
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
      kategorija: s.kategorija,
      grupa: s.grupa,
      redGrupe: s.redGrupe,
    };
    r.kolicina = zaokruzi(r.kolicina + s.kolicina);
    r.artikalObrisan ||= s.artikalObrisan;
    if (s.nabavnaCijena && s.nabavnaCijena > 0) r.trosak = zaokruzi((r.trosak ?? 0) + s.kolicina * s.nabavnaCijena, 2);
    redovi.set(kljuc, r);
  }
  return [...redovi.values()].sort((a, b) => a.artikal.localeCompare(b.artikal, "sr-Latn"));
}

/**
 * Spisak razvrstan po vrsti robe: grupe redom kojim ih server rangira (korpice redom tipova,
 * Špajz posljednji), artikli u grupi ostaju po nazivu. Svaki artikal je u tačno jednoj grupi.
 */
export function grupeNabavke(redovi: RedNabavke[]): GrupaNabavke[] {
  const grupe = new Map<string, GrupaNabavke & { red: number }>();
  for (const r of redovi) {
    const g = grupe.get(r.grupa) ?? { naziv: r.grupa, redovi: [], trosak: null, red: r.redGrupe };
    g.redovi.push(r);
    if (r.trosak !== null) g.trosak = zaokruzi((g.trosak ?? 0) + r.trosak, 2);
    g.red = Math.min(g.red, r.redGrupe);
    grupe.set(r.grupa, g);
  }
  return [...grupe.values()]
    .sort((a, b) => a.red - b.red || a.naziv.localeCompare(b.naziv, "sr-Latn"))
    .map(({ naziv, redovi, trosak }) => ({ naziv, redovi, trosak }));
}
