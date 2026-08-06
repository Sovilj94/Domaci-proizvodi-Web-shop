/** Shared payload validation for the mock product endpoints. */
export function validateProduct(body: unknown): Record<string, string[]> {
  const details: Record<string, string[]> = {};
  const b = body as Record<string, unknown> | null;
  if (!b?.name) details.name = ["Naziv je obavezan."];
  if (typeof b?.price !== "number" || (b.price as number) <= 0) {
    details.price = ["Cijena mora biti veća od 0."];
  }
  if (!b?.unit) details.unit = ["Jedinica je obavezna."];
  if (!b?.categoryId) details.categoryId = ["Kategorija je obavezna."];
  if (!b?.producerId) details.producerId = ["Proizvođač je obavezan."];
  return details;
}
