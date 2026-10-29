"use client";

import { useEffect, useRef, useState } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { postaviJezik, procitajJezik, type Jezik } from "@/lib/jezik/jezik";

const JEZICI: { jezik: Jezik; naziv: string; Zastava: () => React.JSX.Element }[] = [
  { jezik: "sr", naziv: "Srpski", Zastava: ZastavaSrbije },
  { jezik: "en", naziv: "English", Zastava: ZastavaUK },
];

/**
 * Izbor jezika sa zastavicom (po uzoru na farmit: „🇷🇸 Srpski ▾"). Natpisi nose `translate="no"`
 * da ih prevođenje ne dira. Promjena ponovo učitava stranicu: povratak na srpski bez toga bi
 * tražio čuvanje originalnog teksta svakog čvora. Zastave su SVG — emoji zastave Windows ne crta.
 */
export function JezikPrekidac({ className, naMeniju = false }: { className?: string; naMeniju?: boolean }) {
  const [jezik, setJezik] = useState<Jezik>("sr");
  const [otvoren, setOtvoren] = useState(false);
  const okvir = useRef<HTMLDivElement>(null);

  // Kolačić se čita tek na klijentu; server uvijek renderuje srpski.
  useEffect(() => setJezik(procitajJezik()), []);

  useEffect(() => {
    if (!otvoren) return;
    const klik = (e: MouseEvent) => {
      if (!okvir.current?.contains(e.target as Node)) setOtvoren(false);
    };
    document.addEventListener("mousedown", klik);
    return () => document.removeEventListener("mousedown", klik);
  }, [otvoren]);

  function izaberi(novi: Jezik) {
    setOtvoren(false);
    if (novi === jezik) return;
    postaviJezik(novi);
    window.location.reload();
  }

  const aktivni = JEZICI.find((j) => j.jezik === jezik)!;

  return (
    <div ref={okvir} className="relative" translate="no">
      <button
        type="button"
        onClick={() => setOtvoren((o) => !o)}
        aria-haspopup="listbox"
        aria-expanded={otvoren}
        className={cn("inline-flex items-center gap-2 transition", className)}
      >
        <aktivni.Zastava />
        {aktivni.naziv}
        <ChevronDown className={cn("h-4 w-4 transition", otvoren && "rotate-180")} />
      </button>
      {otvoren && (
        <ul
          role="listbox"
          className={cn(
            "absolute z-50 mt-1 min-w-[150px] overflow-hidden rounded-xl border border-line bg-white py-1 text-sm text-ink shadow-lg",
            naMeniju ? "left-0" : "right-0",
          )}
        >
          {JEZICI.map(({ jezik: j, naziv, Zastava }) => (
            <li key={j}>
              <button
                type="button"
                role="option"
                aria-selected={j === jezik}
                onClick={() => izaberi(j)}
                className={cn(
                  "flex w-full items-center gap-2 px-3 py-2 text-left hover:bg-sand",
                  j === jezik && "font-semibold",
                )}
              >
                <Zastava />
                {naziv}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Trobojka Srbije (crvena, plava, bijela), bez grba — prepoznatljiva i u veličini 20×14. */
function ZastavaSrbije() {
  return (
    <svg viewBox="0 0 30 20" className="h-3.5 w-5 shrink-0 rounded-[2px] shadow-[0_0_0_1px_rgba(0,0,0,0.08)]" aria-hidden>
      <rect width="30" height="20" fill="#fff" />
      <rect width="30" height="13.33" fill="#0C4076" />
      <rect width="30" height="6.67" fill="#C6363C" />
    </svg>
  );
}

/** Zastava Ujedinjenog Kraljevstva, pojednostavljena za malu veličinu. */
function ZastavaUK() {
  return (
    <svg viewBox="0 0 60 30" className="h-3.5 w-5 shrink-0 rounded-[2px] shadow-[0_0_0_1px_rgba(0,0,0,0.08)]" aria-hidden>
      <clipPath id="uk-okvir">
        <rect width="60" height="30" />
      </clipPath>
      <g clipPath="url(#uk-okvir)">
        <rect width="60" height="30" fill="#012169" />
        <path d="M0,0 60,30 M60,0 0,30" stroke="#fff" strokeWidth="6" />
        <path d="M0,0 60,30 M60,0 0,30" stroke="#C8102E" strokeWidth="2.5" />
        <path d="M30,0 V30 M0,15 H60" stroke="#fff" strokeWidth="10" />
        <path d="M30,0 V30 M0,15 H60" stroke="#C8102E" strokeWidth="6" />
      </g>
    </svg>
  );
}
