import type { DeliveryZone } from "@/types/domain";

/**
 * Pogađa zonu dostave po imenima naselja koja geokoder vrati za adresu
 * (`GeocodeHit.localities`). Namjerno „best-effort": ime naselja i ime zone su dva
 * nezavisna rječnika, pa se gleda i podudaranje po dijelu imena. Kad nema pogotka,
 * vraća `null` i izbor ostaje kupcu — pogrešno predložena zona je gora od nikakve,
 * jer je `CreateOrderValidator` odbija tek na checkoutu.
 *
 * Stoji ovdje a ne u checkoutu zato što ga od P2c zovu dva ekrana: checkout i forma
 * adresara. Dvije kopije ovog uslova bi se razišle u prvoj izmjeni.
 */
export function pogodiZonu(
  localities: string[],
  zones: DeliveryZone[] | undefined,
): DeliveryZone | null {
  if (!zones || localities.length === 0) return null;
  return (
    zones.find((z) =>
      localities.some((l) => {
        const a = l.toLowerCase();
        const b = z.name.toLowerCase();
        return a === b || a.includes(b) || b.includes(a);
      }),
    ) ?? null
  );
}
