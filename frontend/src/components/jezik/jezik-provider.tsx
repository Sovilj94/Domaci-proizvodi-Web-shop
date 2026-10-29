"use client";

import { useEffect } from "react";
import { pokreniPrevodjenje } from "@/lib/jezik/dom";
import { procitajJezik } from "@/lib/jezik/jezik";

/**
 * Engleski za kupčev dio sajta. Montira ga samo `(shop)` layout, pa admin ostaje na srpskom.
 * Ne renderuje ništa — prevodi stranicu poslije hidratacije. `data-jezik-spremno` skida CSS
 * skrivanje stranice (vidi `globals.css`).
 */
export function JezikProvider() {
  useEffect(() => {
    if (procitajJezik() !== "en") return;
    const html = document.documentElement;
    // Skripta u <head> je ovo postavila pri punom učitavanju; ovdje zbog klijentske navigacije
    // iz admina u prodavnicu, gdje se <head> skripta ne izvršava ponovo.
    html.setAttribute("data-jezik", "en");
    html.lang = "en";
    // Start tek kad je pregledač slobodan: Suspense granice (npr. stranice sa useSearchParams)
    // hidratišu poslije ovog efekta, a prevod njihovog serverskog HTML-a prije toga bi React
    // vidio kao neslaganje i iscrtao granicu ispočetka. Posmatrač poslije uhvati sve novo.
    let zaustavi = () => {};
    const kreni = () => {
      zaustavi = pokreniPrevodjenje(document.body);
      html.setAttribute("data-jezik-spremno", "");
    };
    const idle = window.requestIdleCallback?.(kreni, { timeout: 400 });
    const tajmer = idle === undefined ? window.setTimeout(kreni, 150) : undefined;
    return () => {
      if (idle !== undefined) window.cancelIdleCallback(idle);
      if (tajmer !== undefined) window.clearTimeout(tajmer);
      zaustavi();
      html.removeAttribute("data-jezik");
      html.removeAttribute("data-jezik-spremno");
      html.lang = "sr-Latn";
    };
  }, []);

  return null;
}
