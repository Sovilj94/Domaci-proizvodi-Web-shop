import { RJECNIK } from "./rjecnik.ts";
import { RJECNIK_STRANICE } from "./rjecnik-stranice.ts";

/*
 * Prevod jednog teksta na engleski: tačan pogodak u rječniku, pa nazivi sa servera (proizvodi,
 * kategorije), pa obrasci za tekst sa brojevima i datumima. Razmaci na krajevima se čuvaju
 * (JSX ih često ostavlja uz <strong>). Nepoznat tekst ostaje kakav jeste.
 */

let dinamicki: Record<string, string> = {};

/** Nazivi i opisi sa servera (proizvodi, kategorije) — šalje ih `GET /prevodi?jezik=en`. */
export function postaviDinamicke(mapa: Record<string, string>): void {
  dinamicki = mapa;
}

const MJESECI: Record<string, string> = {
  januar: "January", februar: "February", mart: "March", april: "April", maj: "May", jun: "June",
  jul: "July", avgust: "August", septembar: "September", oktobar: "October", novembar: "November",
  decembar: "December",
};

const DANI: Record<string, string> = {
  ponedjeljak: "Monday", ponedjeljka: "Monday", utorak: "Tuesday", utorka: "Tuesday", srijeda: "Wednesday",
  srijedu: "Wednesday", srijede: "Wednesday", četvrtak: "Thursday", četvrtka: "Thursday", petak: "Friday",
  petka: "Friday", subota: "Saturday", subotu: "Saturday", subote: "Saturday", nedjelja: "Sunday",
  nedjelju: "Sunday", nedjelje: "Sunday",
};

const dan = (d: string) => DANI[d.toLowerCase()] ?? d;
const dani = (s: string) => s.replace(/\p{L}+/gu, (w) => (w === "ili" ? "or" : w === "i" ? "and" : dan(w)));
const mn = (n: string, jed: string, vise: string) => (n === "1" ? jed : vise);

/** [obrazac, prevod] — obrazac je cijeli tekst (sa ^ i $). */
const OBRASCI: [RegExp, (...g: string[]) => string][] = [
  [/^(\d+) (dostava|dostave)$/, (n) => `${n} ${mn(n, "delivery", "deliveries")}`],
  [/^(\d+) (isporuka|isporuke)$/, (n) => `${n} ${mn(n, "delivery", "deliveries")}`],
  [/^\/(\d+) (korpice|korpica)$/, (n) => `/${n} boxes`],
  [/^([\d.,]+ KM) po korpici · plaćaš pri svakoj dostavi$/, (c) => `${c} per box · pay at each delivery`],
  [/^Obaveza (\d+) (?:isporuka|isporuke) · plaćanje pouzećem$/, (n) => `${n}-delivery commitment · cash on delivery`],
  [/^(Mala|Srednja|Velika) korpica (povrća|voća)$/, (v, t) =>
    `${{ Mala: "Small", Srednja: "Medium", Velika: "Large" }[v]} ${t === "povrća" ? "vegetable" : "fruit"} box`],
  [/^Korpica (povrća|voća)$/, (t) => (t === "povrća" ? "Vegetable box" : "Fruit box")],
  [/^Korpice (povrća|voća)$/, (t) => (t === "povrća" ? "Vegetable boxes" : "Fruit boxes")],
  [/^Ove sedmice u (maloj|srednjoj|velikoj) korpici…$/, (v) =>
    `This week in the ${{ maloj: "small", srednjoj: "medium", velikoj: "large" }[v]} box…`],
  [/^U (maloj|srednjoj|velikoj) korpici dobijaš najmanje$/, (v) =>
    `The ${{ maloj: "small", srednjoj: "medium", velikoj: "large" }[v]} box has at least`],
  [/^(\d+) namirnica$/, (n) => `${n} items`],
  [/^od ovih, u vrijednosti od$/, () => "of these, worth"],
  [/^(Mala|Srednja|Velika) korpica \| ([\d.,]+ KM)$/, (v, c) =>
    `${{ Mala: "Small", Srednja: "Medium", Velika: "Large" }[v]} box | ${c}`],
  [/^(Mala|Srednja|Velika) korpica$/, (v) => `${{ Mala: "Small", Srednja: "Medium", Velika: "Large" }[v]} box`],
  [/^Svake sedmice na tvoju adresu stiže mješavina svježeg sezonskog (povrća|voća) sa Manjače\.$/, (t) =>
    `Every week a mix of fresh seasonal ${t === "povrća" ? "vegetables" : "fruit"} from Manjača arrives at your address.`],
  [/^Uživaj u svježem i raznovrsnom sezonskom (povrću|voću)\.$/, (t) =>
    `Enjoy fresh and varied seasonal ${t === "povrću" ? "vegetables" : "fruit"}.`],
  [/^Sa nama uvijek znaš koje (povrće|voće) možeš očekivati u narednoj isporuci, u zavisnosti od godišnjeg doba\.$/, (t) =>
    `With us you always know which ${t === "povrće" ? "vegetables" : "fruit"} to expect in the next delivery, depending on the season.`],
  [/^🚚 Poruči do (\p{L}+) u (\d+)h i stiže ti korpica u (.+)$/u, (d, h, kad) =>
    `🚚 Order by ${dan(d)} ${h}:00 and your box arrives on ${dani(kad)}`],
  [/^🚚 Poruči do (\p{L}+) u (\d+)h i stiže ti korpica$/u, (d, h) => `🚚 Order by ${dan(d)} ${h}:00 and your box arrives`],
  [/^Utisak (\d+): (.+)$/, (n, ime) => `Review ${n}: ${ime}`],
  [/^Prikaži utisak (\d+)$/, (n) => `Show review ${n}`],
  [/^Odaberi (.+)$/, (s) => `Choose ${prevediJezgro(s)}`],
  [/^Dodaj (.+) u korpu$/, (s) => `Add ${prevediJezgro(s)} to cart`],
  [/^Uklonjeno (\d+) nedostupnih proizvoda iz korpe\.$/, (n) => `Removed ${n} unavailable products from the cart.`],
  [/^Rezultati za „(.+)"$/, (s) => `Results for “${s}”`],
  [/^Obaveza: još (\d+) od (\d+) (?:isporuka|isporuke)$/, (a, b) => `Commitment: ${a} of ${b} deliveries left`],
  [/^Obaveza od (\d+) (?:isporuka|isporuke) je ispunjena$/, (n) => `${n}-delivery commitment fulfilled`],
  [/^Plan: (.+) · (\d+) (?:isporuka|isporuke) iza tebe$/, (p, n) => `Plan: ${prevediJezgro(p)} · ${n} deliveries so far`],
  [/^(\d+) (narudžba|narudžbe|narudžbi)$/, (n) => `${n} ${mn(n, "order", "orders")}`],
  [/^(\p{L}+), (\d+)$/u, (m, n) => (MJESECI[m.toLowerCase()] ? `${MJESECI[m.toLowerCase()]}, ${n}` : `${m}, ${n}`)],
];

function prevediJezgro(s: string): string {
  const r = RJECNIK[s] ?? RJECNIK_STRANICE[s] ?? dinamicki[s];
  if (r !== undefined) return r;
  for (const [obrazac, f] of OBRASCI) {
    const m = obrazac.exec(s);
    if (m) {
      const p = f(...m.slice(1));
      if (p !== s) return p;
    }
  }
  return s;
}

/** Prevod teksta čvora ili atributa; čuva razmake na početku i kraju. */
export function uEngleski(tekst: string): string {
  const m = /^(\s*)([\s\S]*?)(\s*)$/.exec(tekst);
  if (!m || !m[2]) return tekst;
  const jezgro = m[2].replace(/\s+/g, " ");
  const p = prevediJezgro(jezgro);
  return p === jezgro ? tekst : m[1] + p + m[3];
}
