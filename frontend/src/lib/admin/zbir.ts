import type { Order, OrderStatus, Unit } from "@/types/domain";

/** Statusi koje zbir broji: ono što još treba spremiti. Od pakovanja nadalje roba je već u kutiji. */
export const ZBIR_STATUSI: OrderStatus[] = ["Pending", "Confirmed"];

export interface ZbirRed {
  productId: string;
  naziv: string;
  jedinica: Unit;
  naCekanju: number;
  potvrdjeno: number;
  ukupno: number;
}

export interface Zbir {
  korpice: ZbirRed[];
  ostalo: ZbirRed[];
}

const zaokruzi = (x: number) => Math.round(x * 100) / 100;

/** Koliko čega traže narudžbe na čekanju i potvrđene — korpice odvojeno od ostalog. */
export function saberi(narudzbe: Order[]): Zbir {
  const redovi = new Map<string, ZbirRed & { jeKorpica: boolean }>();
  for (const o of narudzbe) {
    if (!ZBIR_STATUSI.includes(o.status)) continue;
    for (const s of o.items) {
      let r = redovi.get(s.productId);
      if (!r) {
        r = {
          productId: s.productId,
          naziv: s.productName,
          jedinica: s.unit,
          naCekanju: 0,
          potvrdjeno: 0,
          ukupno: 0,
          jeKorpica: !!s.jeKorpica,
        };
        redovi.set(s.productId, r);
      }
      if (o.status === "Pending") r.naCekanju += s.quantity;
      else r.potvrdjeno += s.quantity;
    }
  }

  const korpice: ZbirRed[] = [];
  const ostalo: ZbirRed[] = [];
  for (const { jeKorpica, ...r } of redovi.values()) {
    const red = {
      ...r,
      naCekanju: zaokruzi(r.naCekanju),
      potvrdjeno: zaokruzi(r.potvrdjeno),
      ukupno: zaokruzi(r.naCekanju + r.potvrdjeno),
    };
    (jeKorpica ? korpice : ostalo).push(red);
  }
  const poNazivu = (a: ZbirRed, b: ZbirRed) => a.naziv.localeCompare(b.naziv, "sr-Latn"); // „sr" je ćirilični red: Š bi došlo prije S
  return { korpice: korpice.sort(poNazivu), ostalo: ostalo.sort(poNazivu) };
}
