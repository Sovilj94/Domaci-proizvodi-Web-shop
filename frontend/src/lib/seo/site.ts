/**
 * Osnovni SEO podaci sajta — jedini izvor za metadata, robots.txt, sitemap.xml i JSON-LD.
 * Samo za serverski kod (layout/page metadata, app/robots.ts, app/sitemap.ts).
 */

export const SITE_NAME = "E piljara";

export const SITE_DESCRIPTION =
  "Svježi domaći proizvodi sa Manjače — spakovani i dostavljeni na kućni prag u Banjoj Luci. Sedmične korpice povrća i voća, sir, med, zimnica i suho meso od provjerenih domaćina.";

export const SITE_KEYWORDS = [
  "domaći proizvodi",
  "domaće povrće",
  "domaće voće",
  "Manjača",
  "Banja Luka",
  "dostava hrane Banja Luka",
  "korpica povrća",
  "korpica voća",
  "pijaca online",
  "domaći sir",
  "domaći med",
  "zimnica",
];

/** Podrazumijevana slika za dijeljenje (Facebook, Viber, WhatsApp, X). */
export const OG_IMAGE = { url: "/slike/izgled/hero-domacinstvo.jpg", alt: "Domaćinstvo na Manjači" };

/**
 * Javna adresa frontenda, bez kose crte na kraju. Redom: NEXT_PUBLIC_SITE_URL (postavlja se
 * ručno), produkcijski domen Vercel projekta (Vercel ga daje sam), pa localhost.
 */
export const SITE_URL = (
  process.env.NEXT_PUBLIC_SITE_URL ||
  (process.env.VERCEL_PROJECT_PRODUCTION_URL
    ? `https://${process.env.VERCEL_PROJECT_PRODUCTION_URL}`
    : "http://localhost:3000")
).replace(/\/+$/, "");

/**
 * Smiju li pretraživači indeksirati sajt. Podrazumijevano NE — test deploy (Vercel + Render)
 * ne smije završiti u Google-u sa demo podacima. Na pravom domenu postaviti SEO_INDEX=true.
 */
export const SEO_INDEX = process.env.SEO_INDEX === "true";

export function absoluteUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  return `${SITE_URL}${path.startsWith("/") ? "" : "/"}${path}`;
}

/** Skraćuje tekst na granici riječi, za meta description (~155 znakova). */
export function skratiOpis(tekst: string | undefined | null, max = 155): string | undefined {
  const t = (tekst ?? "").replace(/\s+/g, " ").trim();
  if (!t) return undefined;
  if (t.length <= max) return t;
  const rez = t.slice(0, max - 1);
  const granica = rez.lastIndexOf(" ");
  return `${(granica > max * 0.6 ? rez.slice(0, granica) : rez).replace(/[,.;:\s]+$/, "")}…`;
}
