import type { Korpica, PlanPretplate, TipKorpice, VelicinaKorpice } from "@/types/domain";

/*
 * Planovi i ponuda korpica (R2, po uzoru na farmit: NEDELJNA · MESEČNA · 3 MESECA).
 * Cijene i obaveze šalje server (`KorpicaDto`) — ovdje se samo biraju, nikad ne računaju.
 */

export type KljucPlana = "nedjeljna" | "mjesecna" | "tri-mjeseca";

export const PLANOVI: { kljuc: KljucPlana; plan: PlanPretplate; oznaka: string; naziv: string }[] = [
  { kljuc: "nedjeljna", plan: "Nedjeljna", oznaka: "Nedjeljna", naziv: "Nedjeljna pretplata" },
  { kljuc: "mjesecna", plan: "Mjesecna", oznaka: "Mjesečna", naziv: "Mjesečna pretplata" },
  { kljuc: "tri-mjeseca", plan: "TriMjeseca", oznaka: "3 mjeseca", naziv: "Pretplata na 3 mjeseca" },
];

export const PLAN_TEKST: Record<PlanPretplate, string> = {
  Nedjeljna: "Nedjeljna",
  Mjesecna: "Mjesečna",
  TriMjeseca: "3 mjeseca",
};

/** `?plan=` iz URL-a; nepoznato ili izostalo = nedjeljna. */
export function planIzUrla(vrijednost: string | null | undefined): (typeof PLANOVI)[number] {
  return PLANOVI.find((p) => p.kljuc === vrijednost) ?? PLANOVI[0];
}

/** Cijena po isporuci za plan; bez podatka sa servera — puna cijena. */
export function cijenaPlana(k: Korpica, plan: PlanPretplate): number {
  const c = plan === "Mjesecna" ? k.cijenaMjesecna : plan === "TriMjeseca" ? k.cijenaTriMjeseca : k.cijenaNedjeljna;
  return c ?? k.cijena;
}

export function obavezaPlana(k: Korpica, plan: PlanPretplate): number {
  return plan === "Mjesecna" ? (k.obavezaMjesecna ?? 4) : plan === "TriMjeseca" ? (k.obavezaTriMjeseca ?? 13) : 0;
}

/** Vrste korpica koje se prodaju (odluka 2026-10-16: samo povrće i voće). */
export const VRSTE: { tip: TipKorpice; naziv: string; emoji: string; mnozina: string }[] = [
  { tip: "povrce", naziv: "povrće", emoji: "🥦", mnozina: "povrća" },
  { tip: "voce", naziv: "voće", emoji: "🍎", mnozina: "voća" },
];

export function jeVrsta(s: string): s is "povrce" | "voce" {
  return VRSTE.some((v) => v.tip === s);
}

export const VELICINE: { velicina: VelicinaKorpice; naziv: string; lokativ: string; clanovi: string }[] = [
  { velicina: "mala", naziv: "Mala", lokativ: "maloj", clanovi: "za 1 do 2 člana" },
  { velicina: "srednja", naziv: "Srednja", lokativ: "srednjoj", clanovi: "za 2 do 4 člana" },
  { velicina: "velika", naziv: "Velika", lokativ: "velikoj", clanovi: "za 5 i više članova" },
];

/** „N isporuka" sa pravim oblikom: 1 isporuka, 2–4 isporuke, 5+ i 11–14 isporuka. */
export function isporuka(n: number): string {
  const j = n % 10;
  const d = n % 100;
  if (j === 1 && d !== 11) return `${n} isporuka`;
  if (j >= 2 && j <= 4 && (d < 12 || d > 14)) return `${n} isporuke`;
  return `${n} isporuka`;
}

export function jeVelicina(s: string): s is VelicinaKorpice {
  return VELICINE.some((v) => v.velicina === s);
}
