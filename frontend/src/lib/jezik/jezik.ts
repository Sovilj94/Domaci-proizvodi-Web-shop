/*
 * Jezik sajta (srpski | engleski, po uzoru na farmit). Izbor živi u kolačiću `jezik`, kao ranije
 * pismo: root layout ga ne čita (`cookies()` bi sve statičke stranice pretvorio u dinamičke).
 * Server uvijek renderuje srpski; prije prvog iscrtavanja inline skripta u <head> postavi `lang`
 * i `data-jezik`, a poslije hidratacije `JezikProvider` prevede stranicu nad DOM-om.
 */

export type Jezik = "sr" | "en";

const KOLACIC = "jezik";

export function procitajJezik(): Jezik {
  if (typeof document === "undefined") return "sr";
  return /(?:^|;\s*)jezik=en(?:;|$)/.test(document.cookie) ? "en" : "sr";
}

export function postaviJezik(jezik: Jezik): void {
  document.cookie = `${KOLACIC}=${jezik}; max-age=31536000; path=/; SameSite=Lax`;
}

/**
 * Skripta za <head>: postavlja `data-jezik` i `lang` prije prvog iscrtavanja, da CSS sakrije
 * srpski tekst dok `JezikProvider` ne prevede stranicu. Admin ostaje na srpskom.
 * Mora ostati samostalna (nema importa) — ide u stranicu kao tekst.
 */
export const JEZIK_SKRIPTA = `(function(){try{if(/(?:^|;\\s*)jezik=en(?:;|$)/.test(document.cookie)&&location.pathname.indexOf("/admin")!==0){var h=document.documentElement;h.setAttribute("data-jezik","en");h.setAttribute("lang","en");}}catch(e){}})();`;
