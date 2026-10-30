import type { Korpica, Product, Unit } from "@/types/domain";

/** Od ove zalihe naniže artikal je „pri kraju" — isti prag kao „Još malo" na stranici proizvoda. */
export const MALO_ZALIHE = 10;

export type Oznaka = "ok" | "malo" | "rasprodato" | "vanProdaje";

export interface StanjeRed {
  id: string;
  naziv: string;
  ostalo: number;
  jedinica: Unit;
  oznaka: Oznaka;
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
  if (ostalo <= 0) return "rasprodato";
  return ostalo <= MALO_ZALIHE ? "malo" : "ok";
}

/**
 * Koliko je čega ostalo za prodaju. Zaliha se umanjuje već pri narudžbi (CreateOrder),
 * pa je ovo ono što još nije obećano nijednom kupcu.
 */
export function stanje(korpice: Korpica[], proizvodi: Product[]): Stanje {
  return {
    // Korpica sa zalihom a bez `dostupna` nije u ponudi — ne zanima nikoga. Rasprodata ostaje.
    // Redoslijed ostaje serverov (vrsta, pa mala → srednja → velika), ne abecedni.
    korpice: korpice
      .filter((k) => k.dostupna || k.stock <= 0)
      .map((k) => ({ id: k.id, naziv: k.naziv, ostalo: zaokruzi(k.stock), jedinica: "kom" as const, oznaka: oznaka(k.stock, true) })),
    spajz: proizvodi
      .map((p) => ({ id: p.id, naziv: p.name, ostalo: zaokruzi(p.stock), jedinica: p.unit, oznaka: oznaka(p.stock, p.isAvailable) }))
      .sort(poNazivu),
  };
}
