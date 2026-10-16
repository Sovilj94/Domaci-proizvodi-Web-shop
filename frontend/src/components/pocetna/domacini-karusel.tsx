"use client";

import Image from "next/image";
import Link from "next/link";
import { Karusel } from "@/components/pocetna/karusel";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { useProducers } from "@/lib/api/hooks";

/**
 * „Ovo su tvoji domaćini" (R1, po uzoru na farmit): bijele kartice sa „ušicom" gore desno,
 * okrugla fotografija sa zelenim okvirom, mjesto i naziv domaćinstva.
 */
export function DomaciniKarusel() {
  const { data } = useProducers();
  if (!data?.length) return null;

  return (
    <Sekcija ton="krem" id="domacini">
      <SectionHeader naslov="Ovo su tvoji domaćini 🧑‍🌾" />
      <Karusel stavka="w-[82%] sm:w-[calc((100%-4rem)/3)]">
        {data.map((p) => (
          <Link key={p.id} href={`/proizvodjaci/${p.id}`} className="group relative mt-6 block">
            {/* Ušica: izdignuti dio kartice gore desno, kao na uzoru. */}
            <span className="absolute -top-6 right-0 h-12 w-[45%] rounded-t-[26px] bg-white" aria-hidden />
            <div className="relative rounded-[26px] bg-white px-6 pb-8 pt-6 text-center transition group-hover:scale-[1.02]">
              <div className="relative mx-auto aspect-square w-[78%] max-w-[240px] overflow-hidden rounded-full border-4 border-brand-600 bg-sand">
                {p.photoUrl ? (
                  <Image src={p.photoUrl} alt={p.name} fill sizes="240px" className="object-cover" />
                ) : (
                  <span className="grid h-full place-items-center text-6xl" aria-hidden>
                    🧑‍🌾
                  </span>
                )}
              </div>
              <p className="mt-5 text-xs uppercase tracking-wide text-muted">{p.village}</p>
              <p className="mt-1 text-xl font-semibold leading-snug text-ink">{p.name}</p>
            </div>
          </Link>
        ))}
      </Karusel>
    </Sekcija>
  );
}
