"use client";

import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { buttonVariants } from "@/components/ui/button";
import { useBlokovi } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

const TRAJANJE_MS = 6000;

/**
 * Hero karusel preko cijelog ekrana (R1, po uzoru na farmit): fotografija sa tamnim
 * prelivom, veliki bijeli naslov, bijela pilula sa emoji, strelice i traka napretka.
 * Slajd se mijenja svakih 6 s; prelazak mišem preko heroja pauzira. Slajdovi su u bazi
 * (sekcija „pocetna-slajdovi"); bez njih se hero ne prikazuje.
 */
export function HeroKarusel() {
  const { blokovi: slajdovi, isLoading } = useBlokovi("pocetna-slajdovi");
  const [aktivan, setAktivan] = useState(0);
  const [pauza, setPauza] = useState(false);

  useEffect(() => {
    if (pauza || slajdovi.length < 2) return;
    const id = setTimeout(() => setAktivan((a) => (a + 1) % slajdovi.length), TRAJANJE_MS);
    return () => clearTimeout(id);
  }, [aktivan, pauza, slajdovi.length]);

  if (slajdovi.length === 0) {
    // Dok slajdovi stižu, tamna površina čuva visinu; ako ih nema, hero ne postoji.
    return isLoading ? <section className="h-[100svh] min-h-[560px] max-h-[880px] bg-ink" aria-hidden /> : null;
  }

  const idi = (smjer: number) => setAktivan((a) => (a + smjer + slajdovi.length) % slajdovi.length);

  return (
    <section
      className="relative h-[100svh] min-h-[560px] max-h-[880px] overflow-hidden bg-ink"
      onMouseEnter={() => setPauza(true)}
      onMouseLeave={() => setPauza(false)}
      aria-roledescription="karusel"
    >
      {slajdovi.map((s, i) => (
        <div
          key={s.id}
          // Neaktivni slajdovi su samo providni i leže preko aktivnog — bez pointer-events-none
          // klik na „Pogledaj korpice" uhvati dugme zadnjeg slajda (Špajz).
          className={cn(
            "absolute inset-0 transition-opacity duration-700",
            i === aktivan ? "opacity-100" : "pointer-events-none opacity-0",
          )}
          aria-hidden={i !== aktivan}
        >
          {s.slikaUrl && <Image src={s.slikaUrl} alt="" fill priority={i === 0} sizes="100vw" className="object-cover" />}
          <div className="absolute inset-0 bg-black/45" />
          <div className="relative flex h-full flex-col items-center justify-center px-6 text-center text-white">
            <h1 className="text-[38px] font-bold leading-tight sm:text-6xl">{s.naslov}</h1>
            {s.tekst && <p className="mt-6 max-w-2xl text-lg leading-relaxed text-white/95 sm:text-2xl">{s.tekst}</p>}
            {s.dugme && s.link && (
              <Link
                href={s.link}
                tabIndex={i === aktivan ? 0 : -1}
                className={cn(buttonVariants({ variant: "bijelo", size: "lg" }), "mt-14 h-14 px-8 text-lg")}
              >
                {s.dugme}
              </Link>
            )}
            <p className="mt-8 text-sm text-white/90">Plaćaš pouzećem, pri dostavi.</p>
          </div>
        </div>
      ))}

      <button
        onClick={() => idi(-1)}
        aria-label="Prethodni slajd"
        className="absolute left-4 top-1/2 hidden -translate-y-1/2 text-white/80 hover:text-white sm:block"
      >
        <ChevronLeft className="h-9 w-9" />
      </button>
      <button
        onClick={() => idi(1)}
        aria-label="Sljedeći slajd"
        className="absolute right-4 top-1/2 hidden -translate-y-1/2 text-white/80 hover:text-white sm:block"
      >
        <ChevronRight className="h-9 w-9" />
      </button>

      {/* Traka napretka: segment po slajdu, aktivni se puni bijelom. */}
      <div className="absolute inset-x-6 bottom-8 mx-auto flex max-w-5xl gap-1 sm:inset-x-16">
        {slajdovi.map((s, i) => (
          <button
            key={s.id}
            onClick={() => setAktivan(i)}
            aria-label={`Slajd ${i + 1}`}
            className="h-1 flex-1 overflow-hidden rounded-full bg-white/35"
          >
            <span
              className={cn("block h-full bg-white", i < aktivan ? "w-full" : "w-0")}
              style={
                i === aktivan
                  ? { width: "100%", transition: pauza ? "none" : `width ${TRAJANJE_MS}ms linear` }
                  : undefined
              }
            />
          </button>
        ))}
      </div>
    </section>
  );
}
