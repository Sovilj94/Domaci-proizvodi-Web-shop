import type { DostavaPravila } from "@/types/domain";

/**
 * Cijena dostave kakvu će server naplatiti (DostavaPravila na backendu): narudžba sa bar jednom
 * korpicom ima dostavu po `dostavaSaKorpicom`, ostale po `dostavaBezKorpice`. Naselje ne utiče.
 * `null` dok se pravila učitavaju — prikaz tada ne izmišlja iznos.
 */
export function cijenaDostave(saKorpicom: boolean, pravila: DostavaPravila | undefined): number | null {
  if (!pravila) return null;
  return saKorpicom ? pravila.dostavaSaKorpicom : pravila.dostavaBezKorpice;
}

/** Korpa nosi korpicu kad je bar jedna stavka korpica (Product sa Vrsta == Korpica). */
export function imaKorpicu(productIds: readonly string[], korpicaIds: ReadonlySet<string>): boolean {
  return productIds.some((id) => korpicaIds.has(id));
}
