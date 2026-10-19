/*
 * Izbor pisma živi u kolačiću `pismo` ("lat" | "cir"), a ne na serveru: root layout ga
 * NE čita, jer bi `cookies()` tamo pretvorio sve statičke stranice u dinamičke. Prije
 * prvog iscrtavanja ga čita inline skripta u <head> (`app/layout.tsx`), a poslije
 * hidratacije `PismoProvider`.
 */

export type Pismo = "lat" | "cir";

const KOLACIC = "pismo";

export function procitajPismo(): Pismo {
  if (typeof document === "undefined") return "lat";
  return /(?:^|;\s*)pismo=cir(?:;|$)/.test(document.cookie) ? "cir" : "lat";
}

export function postaviPismo(pismo: Pismo): void {
  document.cookie = `${KOLACIC}=${pismo}; max-age=31536000; path=/; SameSite=Lax`;
}

/** Događaj kojim se `PismoProvider` obavještava da je pismo promijenjeno bez ponovnog učitavanja. */
export const DOGADJAJ_PISMA = "epiljara:pismo";

/**
 * Prijava na novom uređaju (A6): kupac koji na nalogu ima ćirilicu, a na ovom uređaju još nije
 * birao pismo, dobija ćirilicu odmah. Izričit izbor na uređaju (kolačić postoji) ima prednost.
 */
export function preuzmiPismoNaloga(pismoNaloga: "Latinica" | "Cirilica" | undefined): void {
  if (typeof document === "undefined" || pismoNaloga !== "Cirilica") return;
  if (/(?:^|;\s*)pismo=/.test(document.cookie)) return;
  postaviPismo("cir");
  window.dispatchEvent(new Event(DOGADJAJ_PISMA));
}

/** Vrijednost enuma `Pismo` na API-ju (PascalCase string). */
export function pismoNaZici(pismo: Pismo): "Latinica" | "Cirilica" {
  return pismo === "cir" ? "Cirilica" : "Latinica";
}

/**
 * Skripta za <head>: postavlja `data-pismo` i `lang` prije prvog iscrtavanja, da CSS
 * sakrije latinicu dok `PismoProvider` ne presloži stranicu. Admin ostaje latinica.
 * Mora ostati samostalna (nema importa) — ide u stranicu kao tekst.
 */
export const PISMO_SKRIPTA = `(function(){try{if(/(?:^|;\s*)pismo=cir(?:;|$)/.test(document.cookie)&&location.pathname.indexOf("/admin")!==0){var h=document.documentElement;h.setAttribute("data-pismo","cir");h.setAttribute("lang","sr-Cyrl");}}catch(e){}})();`;
