import { bezKvacica } from "./proizvodi-tabela.ts";

/** Događaj kojim dugme u meniju otvara Ctrl+K paletu (paleta živi u layoutu, dugme u sidebaru). */
export const OTVORI_PALETU = "epiljara:admin-paleta";

/**
 * Koliko tekst odgovara upitu u Ctrl+K paleti: 3 = počinje upitom, 2 = neka riječ počinje
 * upitom, 1 = sadrži ga, 0 = ne odgovara. Bez obzira na velika slova i kvačice („cvarci" nađe
 * „Čvarci"). Više riječi u upitu — sve moraju odgovarati, ocjena je najslabija od njih.
 */
export function ocjena(tekst: string, upit: string): number {
  const t = bezKvacica(tekst);
  const rijeci = bezKvacica(upit).split(/\s+/).filter(Boolean);
  if (rijeci.length === 0) return 1;
  let najslabija = 3;
  for (const r of rijeci) {
    const o = t.startsWith(r) ? 3 : new RegExp(`(^|[\\s\\-·(/])${escape(r)}`).test(t) ? 2 : t.includes(r) ? 1 : 0;
    if (o === 0) return 0;
    najslabija = Math.min(najslabija, o);
  }
  return najslabija;
}

function escape(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** Najboljih `max` stavki za upit; jednaka ocjena zadržava ulazni redoslijed. */
export function rangiraj<T>(stavke: T[], upit: string, tekst: (s: T) => string, max = 6): T[] {
  return stavke
    .map((s, i) => ({ s, i, o: ocjena(tekst(s), upit) }))
    .filter((x) => x.o > 0)
    .sort((a, b) => b.o - a.o || a.i - b.i)
    .slice(0, max)
    .map((x) => x.s);
}
