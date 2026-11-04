import type { TroskoviPoslovanja } from "@/lib/api/contracts";

export const MJESECI = [
  "januar", "februar", "mart", "april", "maj", "jun",
  "jul", "avgust", "septembar", "oktobar", "novembar", "decembar",
];

/** Promjena u % (jedna decimala) u odnosu na prethodnu vrijednost; null kad prethodne nema. */
export function promjena(sada: number, prije: number): number | null {
  if (!prije) return null;
  return Math.round(((sada - prije) / Math.abs(prije)) * 1000) / 10;
}

/** Udio 0–1 kao „61,9%"; prazno je „—". */
export function procenat(x: number | null | undefined): string {
  if (x === null || x === undefined) return "—";
  return `${(x * 100).toLocaleString("sr-Latn", { maximumFractionDigits: 1 })}%`;
}

export function susjedniMjesec(godina: number, mjesec: number, pomak: number): { godina: number; mjesec: number } {
  const i = godina * 12 + (mjesec - 1) + pomak;
  return { godina: Math.floor(i / 12), mjesec: (i % 12) + 1 };
}

/** Najmanji broj oblika 1, 2 ili 5 × 10^n koji je ≥ x (za vrh ose). */
export function zaokruziGore(x: number): number {
  if (x <= 0) return 1;
  const red = 10 ** Math.floor(Math.log10(x));
  for (const k of [1, 2, 5, 10]) if (k * red >= x) return k * red;
  return 10 * red;
}

/** Vrh ose i pet podjela (0, ¼, ½, ¾, vrh) za stubičasti grafikon. */
export function skalaGrafikona(vrijednosti: number[]): { max: number; podjele: number[] } {
  const max = zaokruziGore(Math.max(0, ...vrijednosti));
  return { max, podjele: [0, 1, 2, 3, 4].map((i) => (max * i) / 4) };
}

export interface RedDobiti {
  stavka: string;
  /** Prihod pozitivan, trošak negativan, dobit sa svojim znakom. */
  iznos: number;
  vrsta: "prihod" | "trosak" | "dobit";
}

const km = (x: number) => x.toLocaleString("sr-Latn", { maximumFractionDigits: 1 });

/**
 * Račun dobiti red po red: neto − nabavna roba − ambalaža − gorivo − ostali troškovi po vrsti.
 * Vrste označene kao gorivo nisu posebni redovi — već su u redu goriva (računi ili dnevne ture).
 */
export function redoviDobiti(prihod: { neto: number; nabavno: number }, t: TroskoviPoslovanja): RedDobiti[] {
  const g = t.gorivo;
  const gorivo =
    g.izvor === "racuni" ? "Gorivo — po računima" : `Gorivo — po dnevnim turama (${km(g.km)} km, ${g.brojDana} ${g.brojDana === 1 ? "vožnja" : g.brojDana >= 2 && g.brojDana <= 4 ? "vožnje" : "vožnji"})`;
  return [
    { stavka: "Neto prihod", iznos: prihod.neto, vrsta: "prihod" },
    { stavka: "Nabavna roba", iznos: -prihod.nabavno, vrsta: "trosak" },
    { stavka: `Ambalaža (uz prodaju)${t.ambalazaProcjena ? " *" : ""}`, iznos: -t.ambalaza, vrsta: "trosak" },
    { stavka: gorivo, iznos: -g.uDobiti, vrsta: "trosak" },
    ...t.poVrsti
      .filter((v) => !v.jeGorivo)
      .map((v): RedDobiti => ({
        stavka: v.ponavljajuci > 0 && v.jednokratni === 0 ? `${v.vrsta} (mjesečno)` : v.vrsta,
        iznos: -v.iznos,
        vrsta: "trosak",
      })),
    { stavka: "Dobit", iznos: t.dobit, vrsta: "dobit" },
  ];
}

/** Vrh i dno ose za grafikon koji ima i gubitke: dno je zaokružen najveći minus (0 bez minusa). */
export function skalaSaMinusom(vrijednosti: number[]): { max: number; min: number } {
  const najmanja = Math.min(0, ...vrijednosti);
  return { max: zaokruziGore(Math.max(0, ...vrijednosti)), min: najmanja < 0 ? zaokruziGore(-najmanja) : 0 };
}
