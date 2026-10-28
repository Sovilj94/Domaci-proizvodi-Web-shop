"use client";

import Image from "next/image";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { korpiceApi } from "@/lib/api/endpoints";
import { VELICINE } from "@/lib/planovi";
import { cn, formatPrice } from "@/lib/utils";
import type { KulturaSezone, VelicinaKorpice } from "@/types/domain";

const MJESECI = [
  "Januar", "Februar", "Mart", "April", "Maj", "Jun",
  "Jul", "Avgust", "Septembar", "Oktobar", "Novembar", "Decembar",
];

/**
 * „Šta dobijaš u korpici?" (po uzoru na farmit): mjesec sa strelicama, izbor veličine i
 * obojene pločice kultura koje se tog mjeseca mogu naći u korpici, sa okvirnom količinom.
 * Kreće od tekućeg mjeseca; podaci su sa servera (`/korpice/sezona`).
 */
export function StaDobijas({ vrsta }: { vrsta: "povrce" | "voce" }) {
  const [mjesec, setMjesec] = useState(() => new Date().getMonth() + 1);
  const [velicina, setVelicina] = useState<VelicinaKorpice>("srednja");
  const { data } = useQuery({
    queryKey: ["korpice", "sezona", vrsta],
    queryFn: () => korpiceApi.sezona(vrsta),
    staleTime: 10 * 60 * 1000,
  });

  if (!data) return null;
  const kulture = data.mjeseci.find((m) => m.mjesec === mjesec)?.kulture ?? [];
  const opisVelicine = data.velicine.find((v) => v.velicina === velicina);
  const pomjeri = (d: number) => setMjesec((m) => ((m - 1 + d + 12) % 12) + 1);

  return (
    <Sekcija>
      <SectionHeader
        naslov="Šta dobijaš u korpici?"
        opis={`Sa nama uvijek znaš koje ${vrsta === "voce" ? "voće" : "povrće"} možeš očekivati u narednoj isporuci, u zavisnosti od godišnjeg doba.`}
      />

      <div className="mx-auto max-w-[560px]">
        <div className="flex items-center justify-between gap-4">
          <button
            type="button"
            onClick={() => pomjeri(-1)}
            aria-label="Prethodni mjesec"
            className="grid h-10 w-10 place-items-center rounded-full text-ink transition hover:bg-sand"
          >
            <ChevronLeft className="h-6 w-6" />
          </button>
          <h3 className="text-4xl font-bold text-honey-500 sm:text-5xl" aria-live="polite">
            {MJESECI[mjesec - 1]}
          </h3>
          <button
            type="button"
            onClick={() => pomjeri(1)}
            aria-label="Sljedeći mjesec"
            className="grid h-10 w-10 place-items-center rounded-full text-ink transition hover:bg-sand"
          >
            <ChevronRight className="h-6 w-6" />
          </button>
        </div>

        <div className="mx-auto mt-5 flex w-fit rounded-full bg-honey-100 p-1" role="tablist" aria-label="Veličina korpice">
          {VELICINE.map((v) => {
            const da = v.velicina === velicina;
            return (
              <button
                key={v.velicina}
                type="button"
                role="tab"
                aria-selected={da}
                onClick={() => setVelicina(v.velicina)}
                className={cn(
                  "rounded-full px-3 py-1.5 text-sm font-medium transition sm:px-5",
                  da ? "bg-brand-600 text-white" : "text-honey-500 hover:text-honey-600",
                )}
              >
                {v.naziv} korpica
              </button>
            );
          })}
        </div>

        {opisVelicine && (
          <p className="mt-3 text-center text-sm text-ink/80">
            U {VELICINE.find((v) => v.velicina === velicina)?.lokativ} korpici dobijaš najmanje{" "}
            <strong>{opisVelicine.brojNamirnica} namirnica</strong> od ovih, u vrijednosti od{" "}
            <strong>{formatPrice(opisVelicine.vrijednost)}</strong>.
          </p>
        )}

        {kulture.length === 0 ? (
          <p className="mt-8 text-center text-muted">Za ovaj mjesec još nemamo kultura u ponudi.</p>
        ) : (
          <ul className="mt-6 grid grid-cols-3 gap-2 sm:grid-cols-4 sm:gap-3">
            {kulture.map((k) => (
              <li key={k.productId}>
                <PlocicaKulture k={k} kolicina={k.kolicine[velicina]} />
              </li>
            ))}
          </ul>
        )}

        <p className="mt-6 text-center text-xs text-honey-500">
          *Sadržaj korpice je okviran i mijenja se usljed vremenskih prilika i ponude domaćina.
        </p>
      </div>
    </Sekcija>
  );
}

function PlocicaKulture({ k, kolicina }: { k: KulturaSezone; kolicina?: number }) {
  const [pala, setPala] = useState(false);

  return (
    <div
      className="relative flex aspect-square flex-col overflow-hidden rounded-xl p-2"
      style={{ backgroundColor: k.boja ?? "var(--color-beige)" }}
    >
      <span className="relative z-10 text-xs font-semibold leading-tight text-ink sm:text-sm">{k.naziv}</span>
      {kolicina != null && (
        <span className="relative z-10 text-[11px] text-ink/80 sm:text-xs">
          {kolicina} {k.jedinica}
        </span>
      )}
      {k.imageUrl && !pala && (
        <div className="relative mx-auto mt-auto h-[55%] w-[80%] overflow-hidden rounded-lg">
          <Image src={k.imageUrl} alt="" fill sizes="120px" className="object-cover" onError={() => setPala(true)} />
        </div>
      )}
    </div>
  );
}
