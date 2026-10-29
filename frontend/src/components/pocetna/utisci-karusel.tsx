"use client";

import Image from "next/image";
import { useEffect, useState } from "react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";
import { useUtisci } from "@/lib/api/hooks";
import { IZGLED } from "@/lib/izgled";
import { cn } from "@/lib/utils";

const SMJENA_MS = 3000;
const PRELAZ_MS = 300;

/**
 * Utisci kupaca (po uzoru na farmit „Rekli su o farmitu", Swiper sa petljom): traka uspravnih
 * fotografija poravnatih po dnu; aktivna je velika i stoji druga s lijeva. Pri smjeni se cijela
 * traka pomjeri za jedno mjesto (300 ms), a aktivna fotografija istovremeno raste, prethodna se
 * smanjuje — bez preslagivanja. Petlja je beskonačna: traka sadrži tri kopije niza, a kad pređe u
 * krajnju kopiju, bez animacije se vrati u srednju. Smjena svake 3 s, pauza dok je miš iznad
 * (i bez nje uz smanjene animacije). Utisak nema svoju fotografiju — smjenjuju se `IZGLED.utisci`.
 * Bez objavljenih utisaka sekcija se ne prikazuje (spec P4, O2).
 */
export function UtisciKarusel() {
  const { data } = useUtisci();
  const lista = (data ?? []).slice(0, 12);
  const n = lista.length;
  // Pozicija u traci od 3n fotografija; normalno je u srednjoj kopiji [n, 2n).
  const [poz, setPoz] = useState(0);
  const [bezPrelaza, setBezPrelaza] = useState(false);
  const [pauza, setPauza] = useState(false);

  // Kad stignu podaci, aktivna je prva iz srednje kopije.
  useEffect(() => {
    if (n) setPoz((p) => (p >= n && p < 2 * n ? p : n));
  }, [n]);

  useEffect(() => {
    if (n < 2 || pauza) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    const t = window.setInterval(() => setPoz((p) => p + 1), SMJENA_MS);
    return () => window.clearInterval(t);
  }, [n, pauza]);

  // Skok nazad u srednju kopiju poslije animacije, bez prelaza; prelaz se vraća u sljedećem kadru.
  function krajPrelaza() {
    if (poz >= 2 * n || poz < n) {
      setBezPrelaza(true);
      setPoz((p) => (p >= 2 * n ? p - n : p + n));
    }
  }
  useEffect(() => {
    if (!bezPrelaza) return;
    const r = requestAnimationFrame(() => requestAnimationFrame(() => setBezPrelaza(false)));
    return () => cancelAnimationFrame(r);
  }, [bezPrelaza]);

  if (!n) return null;
  const a = ((poz % n) + n) % n;
  const u = lista[a];
  const traka = [...lista, ...lista, ...lista];
  const prelaz = bezPrelaza ? "none" : `all ${PRELAZ_MS}ms ease`;

  return (
    <Sekcija siroka>
      <SectionHeader naslov="Rekli su o E piljari 💭" />

      <div
        className={cn(
          "relative overflow-hidden",
          // Mjere kao na uzoru: mala 150×267 → 100×178 → 130×231 → 160×284; velika 240×426 → 340×569.
          "[--mala-s:150px] [--mala-v:267px] [--velika-s:240px] [--velika-v:426px] [--razmak:16px]",
          "sm:[--mala-s:100px] sm:[--mala-v:178px] sm:[--velika-s:200px] sm:[--velika-v:355px]",
          "md:[--velika-s:250px] md:[--velika-v:444px]",
          "lg:[--mala-s:130px] lg:[--mala-v:231px] lg:[--velika-s:300px] lg:[--velika-v:533px] lg:[--razmak:20px]",
          "xl:[--mala-s:160px] xl:[--mala-v:284px] xl:[--velika-s:340px] xl:[--velika-v:569px]",
        )}
        onMouseEnter={() => setPauza(true)}
        onMouseLeave={() => setPauza(false)}
      >
        <ul
          className="flex h-[var(--velika-v)] items-end gap-[var(--razmak)]"
          style={{
            // Aktivna je druga s lijeva: ispred nje je tačno jedna mala fotografija.
            transform: `translateX(calc(${-(poz - 1)} * (var(--mala-s) + var(--razmak))))`,
            transition: bezPrelaza ? "none" : `transform ${PRELAZ_MS}ms ease`,
          }}
          onTransitionEnd={(e) => {
            if (e.target === e.currentTarget && e.propertyName === "transform") krajPrelaza();
          }}
        >
          {traka.map((x, i) => {
            const da = i === poz;
            return (
              <li
                key={`${x.id}-${i}`}
                className={cn(
                  "shrink-0",
                  da ? "h-[var(--velika-v)] w-[var(--velika-s)]" : "h-[var(--mala-v)] w-[var(--mala-s)]",
                )}
                style={{ transition: prelaz }}
              >
                <button
                  type="button"
                  tabIndex={Math.abs(i - poz) <= n / 2 ? 0 : -1}
                  onClick={() => setPoz(i)}
                  aria-label={`Utisak ${(i % n) + 1}: ${x.ime}`}
                  aria-current={da || undefined}
                  className="relative block h-full w-full overflow-hidden rounded-[30px] bg-sand"
                >
                  <Image
                    src={IZGLED.utisci[(i % n) % IZGLED.utisci.length]}
                    alt=""
                    fill
                    sizes="340px"
                    className="object-cover"
                  />
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
      </div>

      {n > 1 && (
        <div className="mt-8 flex justify-center gap-2.5">
          {lista.map((x, i) => (
            <button
              key={x.id}
              type="button"
              onClick={() => setPoz(n + i)}
              aria-label={`Prikaži utisak ${i + 1}`}
              className={cn("h-2.5 w-2.5 rounded-full transition", i === a ? "bg-honey-500" : "bg-ink/70 hover:bg-ink")}
            />
          ))}
        </div>
      )}
    </Sekcija>
  );
}
