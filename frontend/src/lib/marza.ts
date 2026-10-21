/**
 * Marža kao udio prodajne cijene (0.35 = 35%) — isti račun kao backend
 * (`MappingExtensions.Marza`). Null kad nabavna cijena nije unesena ili prodajna nije veća od 0.
 */
export function marza(prodajna: number, nabavna: number | null | undefined): number | null {
  if (nabavna == null || !Number.isFinite(nabavna) || nabavna <= 0 || prodajna <= 0) return null;
  return Math.round(((prodajna - nabavna) / prodajna) * 10000) / 10000;
}

/** "35%" / "−4%"; crtica kad marže nema. */
export function formatMarza(m: number | null | undefined): string {
  if (m == null) return "—";
  const pct = Math.round(m * 1000) / 10;
  return `${pct < 0 ? "−" : ""}${Math.abs(pct).toLocaleString("sr-Latn-BA")}%`;
}

/** Boja oznake marže: crvena ispod minimuma, žuta do 10 poena iznad, inače zelena. */
export function tonMarze(m: number | null | undefined, minimum = 0.25): "nema" | "losa" | "tijesna" | "dobra" {
  if (m == null) return "nema";
  if (m < minimum) return "losa";
  if (m < minimum + 0.1) return "tijesna";
  return "dobra";
}
