"use client";

import Image from "next/image";
import { useBlokovi } from "@/lib/api/hooks";

/**
 * Fotografija preko cijele širine sa porukom o domaćinima (R1, po uzoru na farmit). Slika, naslov
 * i tekst su u bazi (sekcija „pocetna-domacini"); bez bloka se sekcija ne prikazuje.
 */
export function DomaciniBaner() {
  const { blokovi } = useBlokovi("pocetna-domacini");
  const b = blokovi[0];
  if (!b) return null;
  return (
    <section className="relative flex min-h-[420px] items-center justify-center overflow-hidden bg-ink px-6 py-24 sm:min-h-[560px]">
      {b.slikaUrl && <Image src={b.slikaUrl} alt="" fill sizes="100vw" className="object-cover" />}
      <div className="absolute inset-0 bg-black/40" />
      <div className="relative max-w-2xl text-center text-white">
        {b.naslov && <h2 className="text-[34px] font-bold leading-tight sm:text-6xl">{b.naslov}</h2>}
        {b.tekst && <p className="mt-5 text-base leading-relaxed text-white/95 sm:text-lg">{b.tekst}</p>}
      </div>
    </section>
  );
}
