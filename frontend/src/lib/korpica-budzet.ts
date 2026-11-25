/**
 * Korpica po budžetu u kupčevom editoru: kupac slaže lokalni nacrt (−/+, izbaci, dodaj), a traka
 * pokazuje koliko je korpice iskorišteno. Pravila količine (najmanje, korak) šalje server po
 * stavci — ovdje se samo primjenjuju; server pri čuvanju sve ponovo provjerava (i maržu).
 * Bez uvoza preko `@/` — `node --test` ga ne razrješava.
 */

export interface StavkaNacrta {
  productId: string;
  kolicina: number;
  /** Cijena po jedinici — samo za traku, kupcu se ne prikazuje. */
  cijena: number;
  najmanje: number;
  korak: number;
}

/** Granice budžeta — iste kao `IzmijeniMojuKorpicuHandler.DonjaGranica/GornjaGranica`. */
export const DONJA_GRANICA = 0.95;
export const GORNJA_GRANICA = 1;

/** Zaokruživanje na 3 decimale, da 0,1 + 0,2 ne postane 0,30000000000000004. */
const k3 = (x: number) => Math.round(x * 1000) / 1000;
const km = (x: number) => Math.round(x * 100) / 100;

export function vrijednostNacrta(stavke: StavkaNacrta[]): number {
  return km(stavke.reduce((z, s) => z + s.kolicina * s.cijena, 0));
}

/** Jedan korak više; količina ispod najmanje (korpica sastavljena po starom pravilu) skače na najmanju. */
export function povecaj(s: StavkaNacrta): StavkaNacrta {
  if (s.kolicina < s.najmanje - 1e-9) return { ...s, kolicina: s.najmanje };
  return { ...s, kolicina: k3(s.kolicina + s.korak) };
}

/** Jedan korak manje; null kad bi pala ispod najmanje — tada se namirnica izbacuje. */
export function smanji(s: StavkaNacrta): StavkaNacrta | null {
  const nova = k3(s.kolicina - s.korak);
  return nova < s.najmanje - 1e-9 ? null : { ...s, kolicina: nova };
}

export function procenat(vrijednost: number, cilj: number): number {
  return cilj > 0 ? Math.round((vrijednost / cilj) * 100) : 0;
}

export type StanjeBudzeta = "malo" | "ok" | "previse";

/** Isto zaokruživanje granica kao server (`Math.Round(cilj * 0.95, 2)`). */
export function stanjeBudzeta(vrijednost: number, cilj: number): StanjeBudzeta {
  if (vrijednost < km(cilj * DONJA_GRANICA)) return "malo";
  if (vrijednost > km(cilj * GORNJA_GRANICA)) return "previse";
  return "ok";
}

/**
 * Može li `dodatak` KM ući a da korpica ne pređe 100 % cilja (iste granice kao server). Koristi se da
 * se posive namirnice i „+" koji bi korpicu prepunili. Maržu zna samo server — on je provjerava pri čuvanju.
 */
export function staje(vrijednost: number, dodatak: number, cilj: number): boolean {
  return km(vrijednost + dodatak) <= km(cilj * GORNJA_GRANICA);
}
