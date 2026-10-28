"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { KorpicaKartica } from "@/components/korpice/korpica-kartica";
import { PlanSegment, VrstaSegment } from "@/components/korpice/segmenti";
import { SadrzajModal } from "@/components/korpice/sadrzaj-modal";
import { UtisciKarusel } from "@/components/pocetna/utisci-karusel";
import { CestaPitanja } from "@/components/pocetna/cesta-pitanja";
import { BitnoDaZnas } from "@/components/korpice/bitno-da-znas";
import { StaDobijas } from "@/components/korpice/sta-dobijas";
import { USvakomTrenutku } from "@/components/korpice/u-svakom-trenutku";
import { useKorpiceList } from "@/lib/api/hooks";
import { planIzUrla, VELICINE } from "@/lib/planovi";

/**
 * „Izaberi korpicu" (R2, po uzoru na farmit /gajbice/{vrsta}?plan=): naslov, izbor vrste,
 * izbor plana, tri kartice (Mala/Srednja/Velika) sa cijenom izabranog plana, pa utisci, „Šta je
 * bitno da znaš", sadržaj po mjesecima, „U svakom trenutku možeš da" i česta pitanja.
 */
export function IzborKorpice({ vrsta }: { vrsta: "povrce" | "voce" }) {
  const plan = planIzUrla(useSearchParams().get("plan"));
  const { data, isLoading } = useKorpiceList();
  const [sadrzajSlug, setSadrzajSlug] = useState<string | null>(null);

  const korpice = (data ?? [])
    .filter((k) => k.tip === vrsta && k.dostupna)
    .sort((a, b) => VELICINE.findIndex((v) => v.velicina === a.velicina) - VELICINE.findIndex((v) => v.velicina === b.velicina));

  return (
    <>
      <section className="px-4 pb-16 pt-10 sm:pt-12">
        <div className="mx-auto max-w-6xl text-center">
          <h1 className="text-[32px] font-bold text-ink sm:text-[60px] sm:leading-[60px]">Izaberi korpicu</h1>
          <p className="mx-auto mt-4 max-w-xl text-base text-ink sm:text-xl">
            <strong>Direktno</strong> sa domaćinstava. <strong>Poznato porijeklo.</strong> Samo ono što bismo i
            sami jeli.
          </p>
          <div className="mt-8">
            <VrstaSegment aktivna={vrsta} plan={plan.kljuc} />
          </div>
          <PlanSegment aktivan={plan.kljuc} href={(k) => `/korpice/${vrsta}?plan=${k}`} />
        </div>

        <div className="mx-auto mt-10 flex max-w-6xl flex-col gap-4 lg:flex-row lg:justify-center lg:gap-8">
          {isLoading
            ? Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="skeleton h-28 rounded-2xl lg:h-[560px] lg:w-[310px] lg:rounded-[26px]" />
              ))
            : korpice.map((k) => (
                <KorpicaKartica
                  key={k.id}
                  korpica={k}
                  plan={plan.plan}
                  kljucPlana={plan.kljuc}
                  onSadrzaj={() => setSadrzajSlug(k.slug)}
                />
              ))}
          {!isLoading && korpice.length === 0 && (
            <p className="text-center text-muted">Korpice ove vrste trenutno nisu u ponudi.</p>
          )}
        </div>

        <p className="mt-8 text-center text-xs text-honey-500">
          *Slike su informativnog karaktera, a sadržaj se mijenja u zavisnosti od sezone.
        </p>
      </section>

      <UtisciKarusel />
      <BitnoDaZnas vrsta={vrsta} />
      <StaDobijas vrsta={vrsta} />
      <USvakomTrenutku />
      <CestaPitanja />

      {sadrzajSlug && <SadrzajModal slug={sadrzajSlug} onClose={() => setSadrzajSlug(null)} />}
    </>
  );
}
