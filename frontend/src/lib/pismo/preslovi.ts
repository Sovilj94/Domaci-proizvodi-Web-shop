/*
 * Preslovljavanje srpske latinice u ćirilicu i nazad. Ista pravila žive i u
 * backendu (`Cirilica.cs`); ugovor između njih su vektori u `docs/pismo-vektori.json`
 * — izmjena pravila ide u oba fajla i u vektore, nikad samo u jedan.
 *
 * Fajl je namjerno bez zavisnosti i bez `@/` importa: test ga pokreće golim
 * `node --test`, bez bundlera.
 */

const SLOVA: Record<string, string> = {
  a: "а", b: "б", c: "ц", č: "ч", ć: "ћ", d: "д", đ: "ђ", e: "е", f: "ф", g: "г",
  h: "х", i: "и", j: "ј", k: "к", l: "л", m: "м", n: "н", o: "о", p: "п", r: "р",
  s: "с", š: "ш", t: "т", u: "у", v: "в", z: "з", ž: "ж",
};

const DIGRAFI: Record<string, string> = { lj: "љ", nj: "њ", dž: "џ" };

/*
 * Riječi u kojima je `nj`/`dž` granica prefiksa, a ne jedno slovo: nad-živjeti,
 * pod-župan, kon-jugacija, in-jekcija. `rastav` je indeks prvog slova para koji se
 * NE spaja. Lista je mala namjerno — to su riječi koje se na sajtu prodavnice
 * realno mogu pojaviti; ostatak jezika je pravilan.
 */
const IZUZECI: { prefiks: string; rastav: number }[] = [
  { prefiks: "nadž", rastav: 2 },
  { prefiks: "podž", rastav: 2 },
  { prefiks: "konjug", rastav: 2 },
  { prefiks: "injek", rastav: 1 },
];

/** Strana imena bez q/w/x/y koja bi se inače preslovila. Riječ koja počinje njima ostaje. */
const BRENDOVI = ["Google", "Facebook", "Viber", "OpenStreetMap", "OSM", "Leaflet"];

/** Naziv prodavnice je logotip — ostaje latinicom i usred ćiriličnog teksta. */
const NAZIV = "E piljara";

const TOKEN = /[\p{L}\p{N}@._:/+-]+|[^\p{L}\p{N}@._:/+-]+/gu;
const IMA_SLOVO = /\p{L}/u;

function ostajeLatinica(rijec: string): boolean {
  if (/[qwxyQWXY]/.test(rijec)) return true;
  if (rijec.includes("@") || rijec.includes("://") || rijec.toLowerCase().startsWith("www.")) return true;
  // Kod: samo velika slova, cifre i crtice, i bar jedna cifra ili crtica (EP-2610-0054, UITEST5).
  if (/^[A-ZČĆŠŽĐ0-9-]+$/.test(rijec) && /[0-9-]/.test(rijec) && IMA_SLOVO.test(rijec)) return true;
  return BRENDOVI.some((b) => rijec.startsWith(b));
}

function veliko(s: string): boolean {
  return s !== s.toLowerCase();
}

function slovo(lat: string, cir: string): string {
  return veliko(lat) ? cir.toUpperCase() : cir;
}

function rijecUCirilicu(rijec: string): string {
  const mala = rijec.toLowerCase();
  const izuzetak = IZUZECI.find((i) => mala.startsWith(i.prefiks));
  let out = "";
  for (let i = 0; i < rijec.length; i++) {
    const par = mala.slice(i, i + 2);
    if (DIGRAFI[par] && izuzetak?.rastav !== i) {
      out += slovo(rijec[i], DIGRAFI[par]);
      i++;
      continue;
    }
    const c = SLOVA[mala[i]];
    out += c ? slovo(rijec[i], c) : rijec[i];
  }
  return out;
}

function dioUCirilicu(tekst: string): string {
  return tekst.replace(TOKEN, (t) => (IMA_SLOVO.test(t) && !ostajeLatinica(t) ? rijecUCirilicu(t) : t));
}

/** Latinica → ćirilica. Idempotentno: tekst koji je već na ćirilici izlazi isti. */
export function uCirilicu(tekst: string): string {
  if (!tekst) return tekst;
  return tekst.split(NAZIV).map(dioUCirilicu).join(NAZIV);
}

const NAZAD: Record<string, string> = Object.fromEntries([
  ...Object.entries(SLOVA).map(([l, c]) => [c, l]),
  ...Object.entries(DIGRAFI).map(([l, c]) => [c, l]),
]);

/** Ćirilica → latinica. Jednoznačno; služi za upit pretrage kucan ćirilicom. */
export function uLatinicu(tekst: string): string {
  let out = "";
  for (let i = 0; i < tekst.length; i++) {
    const ch = tekst[i];
    const mala = ch.toLowerCase();
    const l = NAZAD[mala];
    if (!l) {
      out += ch;
    } else if (!veliko(ch)) {
      out += l;
    } else if (l.length === 1) {
      out += l.toUpperCase();
    } else {
      // Љ, Њ, Џ: „LJ" u riječi pisanoj velikim slovima, „Lj" na početku obične riječi.
      const sljedece = tekst[i + 1] ?? "";
      out += IMA_SLOVO.test(sljedece) && veliko(sljedece) ? l.toUpperCase() : l[0].toUpperCase() + l.slice(1);
    }
  }
  return out;
}
