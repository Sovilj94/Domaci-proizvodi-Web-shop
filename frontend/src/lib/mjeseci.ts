/** Nazivi mjeseci u padežima — dio interfejsa, ne podatak. Indeks 1–12 (0 se ne koristi). */
export const MJESECI_GENITIV = [
  "", "januara", "februara", "marta", "aprila", "maja", "juna",
  "jula", "avgusta", "septembra", "oktobra", "novembra", "decembra",
];

export const MJESECI_LOKATIV = [
  "", "januaru", "februaru", "martu", "aprilu", "maju", "junu",
  "julu", "avgustu", "septembru", "oktobru", "novembru", "decembru",
];

/**
 * Popunjava oznake u tekstu bloka „korpice-voce-van-sezone": {od} i {do} genitivom („maja"),
 * {u} lokativom mjeseca početka („maju"), {godina} godinom zadnje sezone.
 */
export function popuniSezonu(tekst: string, s: { od: number; do: number; godina?: number }): string {
  return tekst
    .replaceAll("{od}", MJESECI_GENITIV[s.od] ?? "")
    .replaceAll("{do}", MJESECI_GENITIV[s.do] ?? "")
    .replaceAll("{u}", MJESECI_LOKATIV[s.od] ?? "")
    .replaceAll("{godina}", s.godina ? String(s.godina) : "");
}

/** Godina zadnje završene sezone: ako je kraj sezone ove godine još pred nama, prošla godina. */
export function godinaZadnjeSezone(sezonaDo: number, danas: Date): number {
  const mjesec = danas.getMonth() + 1;
  return mjesec > sezonaDo ? danas.getFullYear() : danas.getFullYear() - 1;
}
