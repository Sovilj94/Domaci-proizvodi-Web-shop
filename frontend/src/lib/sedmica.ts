import type { Sedmica } from "@/types/domain";

// Dva padeža za dane u sedmici — "u" traži akuzativ, "do" traži genitiv, i nisu isti
// oblik (npr. "petak" → "petka", ne "petaku"). Indeks je JS Date.getDay() (0 = nedjelja).
export const DANI_AKUZATIV = ["nedjelju", "ponedjeljak", "utorak", "srijedu", "četvrtak", "petak", "subotu"];
export const DANI_GENITIV = ["nedjelje", "ponedjeljka", "utorka", "srijede", "četvrtka", "petka", "subote"];

/** Sat roka: "16h", ili "16:30h" kad rok nije na puni sat — inače bi „u 16h" lagalo za pola sata. */
function sat(d: Date): string {
  const m = d.getMinutes();
  return m === 0 ? `${d.getHours()}h` : `${d.getHours()}:${String(m).padStart(2, "0")}h`;
}

/** "utorka u 16h" — ide iza "do". */
export function rokTekst(rok: Date): string {
  return `${DANI_GENITIV[rok.getDay()]} u ${sat(rok)}`;
}

/** "četvrtak 15.10." — ide iza "u". */
export function isporukaTekst(d: Date): string {
  return `${DANI_AKUZATIV[d.getDay()]} ${d.getDate()}.${d.getMonth() + 1}.`;
}

/**
 * Krug za koji se TRENUTNO naručuje. Do roka je to tekuća sedmica; poslije roka
 * checkout prodaje za narednu (backend `SedmicaHelper.SedmicaZaNarudzbu`), pa i
 * odbrojavanje, FAQ i footer govore o narednoj.
 */
export function aktivniKrug(s: Sedmica): { rok: Date; isporuka: Date; zaSljedecu: boolean } {
  return s.rokIstekao
    ? { rok: new Date(s.sljedeciRok), isporuka: new Date(s.sljedecaIsporuka), zaSljedecu: true }
    : { rok: new Date(s.rokNarucivanja), isporuka: new Date(s.datumIsporuke), zaSljedecu: false };
}
