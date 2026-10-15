"use client";

import { useEffect } from "react";
import { pokreniPreslovljavanje } from "@/lib/pismo/dom";
import { procitajPismo } from "@/lib/pismo/pismo";

/**
 * Ćirilica za kupčev dio sajta (P7). Montira ga samo `(shop)` layout, pa admin ostaje
 * latinica. Ne renderuje ništa — sav posao je u efektu, koji se izvršava tek poslije
 * hidratacije. `data-pismo-spremno` skida CSS skrivanje stranice (vidi `globals.css`).
 */
export function PismoProvider() {
  useEffect(() => {
    if (procitajPismo() !== "cir") return;
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
  }, []);

  return null;
}
