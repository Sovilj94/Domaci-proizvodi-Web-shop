"use client";

import { useEffect, useState } from "react";
import { pokreniPreslovljavanje } from "@/lib/pismo/dom";
import { DOGADJAJ_PISMA, procitajPismo, type Pismo } from "@/lib/pismo/pismo";

/**
 * Ćirilica za kupčev dio sajta (P7). Montira ga samo `(shop)` layout, pa admin ostaje
 * latinica. Ne renderuje ništa — sav posao je u efektu, koji se izvršava tek poslije
 * hidratacije. `data-pismo-spremno` skida CSS skrivanje stranice (vidi `globals.css`).
 */
export function PismoProvider() {
  // Pismo se čita na klijentu; događaj DOGADJAJ_PISMA (preuzimanje pisma naloga pri prijavi)
  // ga mijenja bez ponovnog učitavanja stranice.
  const [pismo, setPismo] = useState<Pismo | null>(null);

  useEffect(() => {
    setPismo(procitajPismo());
    const promjena = () => setPismo(procitajPismo());
    window.addEventListener(DOGADJAJ_PISMA, promjena);
    return () => window.removeEventListener(DOGADJAJ_PISMA, promjena);
  }, []);

  useEffect(() => {
    if (pismo !== "cir") return;
    const html = document.documentElement;
    // Skripta u <head> je ovo već postavila pri punom učitavanju; ovdje zbog klijentske
    // navigacije iz admina u prodavnicu, gdje se <head> skripta ne izvršava ponovo.
    html.setAttribute("data-pismo", "cir");
    html.lang = "sr-Cyrl";
    const zaustavi = pokreniPreslovljavanje(document.body);
    html.setAttribute("data-pismo-spremno", "");
    return () => {
      zaustavi();
      html.removeAttribute("data-pismo");
      html.removeAttribute("data-pismo-spremno");
      html.lang = "sr-Latn";
    };
  }, [pismo]);

  return null;
}
