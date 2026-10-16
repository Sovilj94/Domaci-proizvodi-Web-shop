"use client";

import Image from "next/image";
import Link from "next/link";
import { Karusel } from "@/components/pocetna/karusel";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { buttonVariants } from "@/components/ui/button";
import { useAnalize } from "@/lib/api/hooks";

/**
 * Laboratorijske analize kao obojene kartice (R1, po uzoru na farmit „Kako znam šta jedem?").
 * Tekst tvrdi samo ono što aplikacija radi — nalazi su javni (pouka iz P5, završna recenzija).
 * Bez objavljenih analiza sekcija se ne prikazuje.
 */
export function AnalizeKarusel() {
  const { data } = useAnalize();
  if (!data?.length) return null;

  return (
    <Sekcija>
      <SectionHeader
        naslov="Šta tačno jedeš?"
        opis="Laboratorijski nalazi za naše namirnice su javni — pogledaj šta je ispitano i kakav je rezultat."
      />
      <div className="mx-auto max-w-[760px]">
        <Karusel stavka="w-[78%] sm:w-[calc((100%-4rem)/3)]">
          {data.slice(0, 9).map((a) => (
            <Link
              key={a.id}
              href="/analize"
              className="flex aspect-[4/5] flex-col justify-end overflow-hidden rounded-[22px] p-4 text-white shadow-kartica transition hover:scale-[1.02]"
              style={{ backgroundColor: a.boja ?? "#9DCB9D" }}
            >
              {a.slika && (
                <div className="relative mx-auto mb-auto mt-2 h-[45%] w-[70%] overflow-hidden rounded-xl">
                  <Image src={a.slika} alt="" fill sizes="200px" className="object-cover" />
                </div>
              )}
              <div className="flex items-center gap-2 text-[11px]">
                <span className="rounded-md bg-white px-2 py-0.5 font-semibold text-brand-700">🌿 {a.proizvod ?? "Namirnica"}</span>
                <span className="truncate border-l border-white/60 pl-2 text-white/95">{a.proizvodjac}</span>
              </div>
              <p className="mt-3 text-base font-semibold leading-snug [text-shadow:0_1px_2px_rgba(0,0,0,0.25)]">
                Izvještaj o ispitivanju – {a.uzorak}
              </p>
            </Link>
          ))}
        </Karusel>
      </div>
      <div className="mt-6 text-center">
        <Link href="/analize" className={buttonVariants({ size: "lg" })}>
          Vidi sve analize
        </Link>
      </div>
    </Sekcija>
  );
}
