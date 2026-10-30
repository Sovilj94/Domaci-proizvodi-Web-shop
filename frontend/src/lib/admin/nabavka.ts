import type { StavkaPakerske } from "@/lib/api/contracts";

export interface GrupaNabavke {
  proizvodjac: string;
  selo: string;
  telefon?: string | null;
  stavke: StavkaPakerske[];
  /** Nabavna vrijednost grupe (artikli bez nabavne cijene se ne broje). */
  trosak: number;
}

const poNazivu = (a: string, b: string) => a.localeCompare(b, "sr-Latn");

/**
 * Nabavka ide po proizvođaču: koga zvati, šta i za koliko. Grupe po nazivu, artikal bez
 * proizvođača na kraju; artikli u grupi po nazivu.
 */
export function poProizvodjacu(stavke: StavkaPakerske[]): GrupaNabavke[] {
  const grupe = new Map<string, GrupaNabavke>();
  for (const s of stavke) {
    const kljuc = `${s.proizvodjac}|${s.selo}`;
    const g = grupe.get(kljuc) ?? { proizvodjac: s.proizvodjac, selo: s.selo, telefon: s.telefon, stavke: [], trosak: 0 };
    g.stavke.push(s);
    if (s.nabavnaCijena && s.nabavnaCijena > 0) g.trosak += s.kolicina * s.nabavnaCijena;
    grupe.set(kljuc, g);
  }
  return [...grupe.values()]
    .map((g) => ({ ...g, trosak: Math.round(g.trosak * 100) / 100, stavke: [...g.stavke].sort((a, b) => poNazivu(a.artikal, b.artikal)) }))
    .sort((a, b) => {
      if (!a.proizvodjac !== !b.proizvodjac) return a.proizvodjac ? -1 : 1;
      return poNazivu(a.proizvodjac, b.proizvodjac);
    });
}
