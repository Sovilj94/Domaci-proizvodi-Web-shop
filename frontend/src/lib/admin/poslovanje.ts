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
