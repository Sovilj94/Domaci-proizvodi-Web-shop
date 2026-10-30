import type { Korpica, Product, Unit } from "@/types/domain";

export type Oznaka = "ok" | "rasprodato" | "vanProdaje";

export interface StanjeRed {
  id: string;
  naziv: string;
  ostalo: number;
  jedinica: Unit;
  oznaka: Oznaka;
  /** Samo korpice: artikal koji prvi ponestane. */
  uskoGrlo?: string;
}

export interface Stanje {
  korpice: StanjeRed[];
  spajz: StanjeRed[];
}

const zaokruzi = (x: number) => Math.round(x * 100) / 100;
// „sr" je ćirilični red, pa bi Š došlo prije S.
const poNazivu = (a: StanjeRed, b: StanjeRed) => a.naziv.localeCompare(b.naziv, "sr-Latn");

function oznaka(ostalo: number, uProdaji: boolean): Oznaka {
  if (!uProdaji) return "vanProdaje";
  return ostalo <= 0 ? "rasprodato" : "ok";
}

/**
 * Raspored za prikaz — brojeve računa server: kapacitet korpica iz sadržaja sedmice i
 * slobodnog stanja (Zalihe.cs), a stanje Špajza već umanjeno za ono što je obećano korpicama.
 */
export function stanje(korpice: Korpica[], proizvodi: Product[]): Stanje {
  return {
    // Korpice koje admin ne drži u ponudi ne zanimaju nikoga. Redoslijed ostaje serverov
    // (vrsta, pa mala → srednja → velika), ne abecedni.
    korpice: korpice
      .filter((k) => k.uProdaji)
      .map((k) => ({
        id: k.id,
        naziv: k.naziv,
        ostalo: zaokruzi(k.stock),
        jedinica: "kom" as const,
        oznaka: oznaka(k.stock, true),
        uskoGrlo: k.uskoGrlo ?? undefined,
      })),
    spajz: proizvodi
      .map((p) => ({ id: p.id, naziv: p.name, ostalo: zaokruzi(p.stock), jedinica: p.unit, oznaka: oznaka(p.stock, p.isAvailable) }))
      .sort(poNazivu),
  };
}
