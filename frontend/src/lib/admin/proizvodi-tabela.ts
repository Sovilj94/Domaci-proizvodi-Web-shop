import type { ProductInput } from "@/lib/api/contracts";
import type { Product } from "@/types/domain";

/**
 * Brza izmjena iz tabele proizvoda šalje isti PUT kao forma. Backend polja koja izostanu
 * (boja, nabavna, slika) ne mijenja, pa se iz proizvoda prepisuje samo ono što je obavezno
 * i ono što je na listi, a `izmjena` preko toga.
 */
export function ulazIzProizvoda(p: Product, izmjena: Partial<ProductInput> = {}): ProductInput {
  return {
    name: p.name,
    price: p.price,
    unit: p.unit,
    stock: p.stock,
    categoryId: p.categoryId,
    producerId: p.producerId,
    description: p.description ?? "",
    isAvailable: p.isAvailable,
    isFeatured: p.isFeatured,
    mozeUKorpicu: p.mozeUKorpicu,
    prioritetUKorpici: p.prioritetUKorpici,
    mjeseci: p.mjeseci ?? [],
    ...izmjena,
  };
}

/** Isto što backend vrati poslije izmjene — za optimističan prikaz dok PUT traje. */
export function primijeni(p: Product, izmjena: Partial<ProductInput>): Product {
  const nabavna = izmjena.ukloniNabavnuCijenu
    ? null
    : izmjena.nabavnaCijena !== undefined
      ? izmjena.nabavnaCijena
      : p.nabavnaCijena;
  const cijena = izmjena.price ?? p.price;
  return {
    ...p,
    price: cijena,
    stock: izmjena.stock ?? p.stock,
    isAvailable: izmjena.isAvailable ?? p.isAvailable,
    mozeUKorpicu: izmjena.mozeUKorpicu ?? p.mozeUKorpicu,
    mjeseci: izmjena.mjeseci ?? p.mjeseci,
    nabavnaCijena: nabavna,
    marza: nabavna != null && nabavna > 0 && cijena > 0
      ? Math.round(((cijena - nabavna) / cijena) * 10000) / 10000
      : null,
  };
}

export const FILTERI = [
  { kljuc: "sve", naziv: "Sve" },
  { kljuc: "bez-zalihe", naziv: "Bez zalihe" },
  { kljuc: "bez-nabavne", naziv: "Bez nabavne" },
  { kljuc: "niska-marza", naziv: "Marža ispod minimuma" },
  { kljuc: "u-sezoni", naziv: "U sezoni" },
  { kljuc: "skriveni", naziv: "Skriveni" },
] as const;

export type Filter = (typeof FILTERI)[number]["kljuc"];

export function jeFilter(v: string | null): v is Filter {
  return FILTERI.some((f) => f.kljuc === v);
}

export function uSezoni(p: Product, mjesec: number): boolean {
  return !p.mjeseci || p.mjeseci.length === 0 || p.mjeseci.includes(mjesec);
}

/** `minimalnaMarza` je pravilo iz baze (Podesavanja); bez njega filter „niska marža" ništa ne nalazi. */
export function prolaziFilter(p: Product, filter: Filter, mjesec: number, minimalnaMarza?: number): boolean {
  switch (filter) {
    case "bez-zalihe":
      return p.isAvailable && p.stock <= 0 && uSezoni(p, mjesec);
    case "bez-nabavne":
      return !!p.mozeUKorpicu && (p.nabavnaCijena == null || p.nabavnaCijena <= 0);
    case "niska-marza":
      return p.marza != null && minimalnaMarza != null && p.marza < minimalnaMarza;
    case "u-sezoni":
      return p.isAvailable && uSezoni(p, mjesec);
    case "skriveni":
      return !p.isAvailable;
    default:
      return true;
  }
}

/** Pretraga po nazivu, proizvođaču i kategoriji, bez obzira na velika slova i kvačice. */
export function odgovaraPretrazi(p: Product, pretraga: string): boolean {
  const q = bezKvacica(pretraga.trim());
  if (!q) return true;
  return [p.name, p.producer?.name ?? "", p.categoryName].some((s) => bezKvacica(s).includes(q));
}

export function bezKvacica(s: string): string {
  return s
    .toLowerCase()
    .replace(/đ/g, "dj")
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "");
}

export const MJESECI_SLOVA = ["J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D"];
export const MJESECI_NAZIVI = [
  "januar", "februar", "mart", "april", "maj", "jun",
  "jul", "avgust", "septembar", "oktobar", "novembar", "decembar",
];

/**
 * Klik na mjesec: uključi/isključi. Svih 12 uključenih = cijele godine (prazan niz); zadnji
 * uključeni mjesec se ne gasi, jer bi prazan niz značio cijelu godinu, a ne nijedan mjesec.
 */
export function prebaciMjesec(mjeseci: number[] | undefined, m: number): number[] {
  const sada = mjeseci && mjeseci.length > 0 ? mjeseci : [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  const novi = sada.includes(m) ? sada.filter((x) => x !== m) : [...sada, m].sort((a, b) => a - b);
  if (novi.length === 0) return sada;
  return novi.length === 12 ? [] : novi;
}
