/**
 * Računi za ekran „Postavke korpica" — isti kao na serveru: cilj sadržaja je cijena korpice ×
 * koeficijent vrijednosti, a generator ga dijeli na najmanji broj kultura te veličine.
 */

export type Velicina = "mala" | "srednja" | "velika";
export const VELICINE: Velicina[] = ["mala", "srednja", "velika"];

/** Vrijednost sadržaja korpice (KM). */
export function ciljSadrzaja(cijenaKorpice: number, koeficijent: number): number {
  return Math.round(cijenaKorpice * koeficijent * 100) / 100;
}

/** Koliko KM otprilike vrijedi jedna kultura u toj veličini korpice. */
export function ciljPoKulturi(cijenaKorpice: number, koeficijent: number, najmanjeKultura: number): number {
  return Math.round((ciljSadrzaja(cijenaKorpice, koeficijent) / Math.max(najmanjeKultura, 1)) * 100) / 100;
}

/** Najviše koliko sadržaj smije koštati u nabavci da korpica zadrži minimalnu maržu. */
export function nabavniBudzet(cijenaKorpice: number, minimalnaMarza: number): number {
  return Math.round(cijenaKorpice * (1 - minimalnaMarza) * 100) / 100;
}

export type Ocjena = "ok" | "malo" | "puno";

/**
 * Da li zadata količina „sjeda" u korpicu: njena vrijednost naspram onoga koliko vrijedi
 * jedna kultura. Ispod pola — kultura se izgubi u kutiji; preko 1,6× — pojede mjesto drugima.
 */
export function ocjenaKolicine(kolicina: number, cijena: number, poKulturi: number): Ocjena {
  if (poKulturi <= 0 || cijena <= 0) return "ok";
  const odnos = (kolicina * cijena) / poKulturi;
  return odnos < 0.5 ? "malo" : odnos > 1.6 ? "puno" : "ok";
}

/**
 * Prijedlog količine: vrijednost jedne kulture podijeljena cijenom, zaokružena na broj koji se
 * da spakovati — komadi cijeli, kilogrami/litre iznad 1 na pola, ispod 1 na desetinu.
 */
export function predlozenaKolicina(poKulturi: number, cijena: number, jedinica: string): number {
  if (cijena <= 0) return jedinica === "kom" ? 1 : 0.1;
  const sirovo = poKulturi / cijena;
  if (jedinica === "kom") return Math.max(1, Math.round(sirovo));
  if (sirovo >= 1) return Math.max(0.5, Math.round(sirovo * 2) / 2);
  return Math.max(0.1, Math.round(sirovo * 10) / 10);
}
