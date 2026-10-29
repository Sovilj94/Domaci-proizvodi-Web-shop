"use client";

import Image from "next/image";
import { useEffect, useState } from "react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { useUtisci } from "@/lib/api/hooks";
import { IZGLED } from "@/lib/izgled";
import { cn } from "@/lib/utils";

const SMJENA_MS = 7000;

/**
 * Utisci kupaca (po uzoru na farmit „Rekli su o farmitu"): niz uspravnih fotografija poravnatih po
 * dnu — aktivna je velika i stoji druga s lijeva, ostale su male. Na desktopu desno gore stoje redni
 * broj i ime (veliko, kapitalke) i tekst utiska. Klik na fotografiju ili tačku je bira; same se
 * smjenjuju svakih 7 s (ne uz smanjene animacije). Utisak nema svoju fotografiju, pa se smjenjuju
 * fotografije iz `IZGLED.utisci`. Bez objavljenih utisaka sekcija se ne prikazuje (spec P4, O2).
 */
export function UtisciKarusel() {
  const { data } = useUtisci();
  const lista = (data ?? []).slice(0, 12);
  const n = lista.length;
  const [aktivan, setAktivan] = useState(0);
  const [pauza, setPauza] = useState(false);

  useEffect(() => {
    if (n < 2 || pauza) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const t = window.setInterval(() => setAktivan((a) => (a + 1) % n), SMJENA_MS);
    return () => window.clearInterval(t);
  }, [n, pauza]);

  if (!n) return null;
  const a = aktivan % n;
  const u = lista[a];
  // Aktivna je druga s lijeva, kao na uzoru; ostale idu redom iza nje.
  const redoslijed = Array.from({ length: n }, (_, k) => (a - 1 + k + n) % n);
  const slika = (i: number) => IZGLED.utisci[i % IZGLED.utisci.length];

  return (
    <Sekcija siroka>
      <SectionHeader naslov="Rekli su o E piljari 💭" />

      <div className="relative" onMouseEnter={() => setPauza(true)} onMouseLeave={() => setPauza(false)}>
        <ul className="no-scrollbar flex items-end gap-4 overflow-x-auto pb-1 lg:gap-5 lg:overflow-visible">
          {redoslijed.map((i) => {
            const da = i === a;
            return (
              <li
                key={lista[i].id}
                className={cn(
                  "shrink-0 transition-all duration-300",
                  da
                    ? "h-[426px] w-[240px] md:h-[444px] md:w-[250px] lg:h-[533px] lg:w-[300px] xl:h-[569px] xl:w-[340px]"
                    : "h-[267px] w-[150px] sm:h-[178px] sm:w-[100px] lg:h-[231px] lg:w-[130px] xl:h-[284px] xl:w-[160px]",
                )}
              >
                <button
                  type="button"
                  onClick={() => setAktivan(i)}
                  aria-label={`Utisak ${i + 1}: ${lista[i].ime}`}
                  aria-current={da || undefined}
                  className="relative block h-full w-full overflow-hidden rounded-[30px] bg-sand"
                >
                  <Image src={slika(i)} alt="" fill sizes={da ? "340px" : "160px"} className="object-cover" />
                </button>
              </li>
            );
          })}
        </ul>

        <figure
          className="mt-6 lg:absolute lg:right-0 lg:top-12 lg:mt-0 lg:w-[420px] xl:right-8 xl:top-20 xl:w-[520px] 2xl:w-[620px]"
          aria-live="polite"
        >
          <figcaption className="text-2xl font-bold uppercase text-ink md:text-[40px] md:leading-[46px]">
            <span className="block">#{a + 1}</span>
            <span className="block">{u.ime}</span>
          </figcaption>
          {u.mjesto && <p className="mt-1 text-sm text-muted">{u.mjesto}</p>}
          <blockquote className="mt-2 text-sm font-medium text-ink md:text-base md:leading-7">{u.tekst}</blockquote>
        </figure>

        {n > 1 && (
          <div className="mt-8 flex justify-center gap-2.5">
            {lista.map((x, i) => (
              <button
                key={x.id}
                type="button"
                onClick={() => setAktivan(i)}
                aria-label={`Prikaži utisak ${i + 1}`}
                className={cn(
                  "h-2.5 w-2.5 rounded-full transition",
                  i === a ? "bg-honey-500" : "bg-ink/70 hover:bg-ink",
                )}
              />
            ))}
          </div>
        )}
      </div>
    </Sekcija>
  );
}
