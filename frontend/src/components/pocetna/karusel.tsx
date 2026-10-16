"use client";

import { Children, useCallback, useEffect, useRef, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Lagani karusel za utiske, analize i domaćine (R1, po uzoru na farmit). Bez biblioteke:
 * horizontalni skrol sa `scroll-snap`, strelice na `md+`, tačkice ispod. Prevlačenje prstom
 * radi samo od sebe. Sa jednim ili nijednim elementom nema kontrola.
 */
export function Karusel({
  children,
  stavka,
  strelice = true,
  className,
}: {
  children: React.ReactNode;
  /** Širina jedne stavke, npr. "w-[85%] sm:w-[48%] lg:w-[31%]". */
  stavka: string;
  strelice?: boolean;
  className?: string;
}) {
  const traka = useRef<HTMLDivElement>(null);
  const djeca = Children.toArray(children);
  const [strana, setStrana] = useState(0);
  const [ukupnoStrana, setUkupno] = useState(1);

  const izmjeri = useCallback(() => {
    const el = traka.current;
    if (!el) return;
    // ceil, ne round: 4 stavke po 3 na ekranu su 1,36 ekrana = 2 strane (uz malu toleranciju za razmake).
    const ukupno = Math.max(1, Math.ceil(el.scrollWidth / el.clientWidth - 0.05));
    setUkupno(ukupno);
    const max = el.scrollWidth - el.clientWidth;
    setStrana(max <= 0 ? 0 : Math.round((el.scrollLeft / max) * (ukupno - 1)));
  }, []);

  useEffect(() => {
    izmjeri();
    window.addEventListener("resize", izmjeri);
    return () => window.removeEventListener("resize", izmjeri);
  }, [izmjeri, djeca.length]);

  function idi(smjer: -1 | 1) {
    const el = traka.current;
    if (el) el.scrollBy({ left: smjer * el.clientWidth, behavior: "smooth" });
  }

  function naStranu(i: number) {
    const el = traka.current;
    if (!el) return;
    const max = el.scrollWidth - el.clientWidth;
    el.scrollTo({ left: ukupnoStrana <= 1 ? 0 : (max * i) / (ukupnoStrana - 1), behavior: "smooth" });
  }

  const kontrole = djeca.length > 1 && ukupnoStrana > 1;

  return (
    <div className={cn("relative", className)}>
      <div
        ref={traka}
        onScroll={izmjeri}
        className="no-scrollbar -mx-4 flex snap-x snap-mandatory scroll-px-4 gap-5 overflow-x-auto scroll-smooth px-4 pb-6 pt-4 sm:gap-8"
      >
        {djeca.map((d, i) => (
          <div key={i} className={cn("shrink-0 snap-start", stavka)}>
            {d}
          </div>
        ))}
      </div>

      {kontrole && strelice && (
        <>
          <button
            onClick={() => idi(-1)}
            aria-label="Prethodno"
            className="absolute -left-12 top-1/2 hidden -translate-y-1/2 text-ink/70 transition hover:text-ink md:block"
          >
            <ChevronLeft className="h-8 w-8" />
          </button>
          <button
            onClick={() => idi(1)}
            aria-label="Sljedeće"
            className="absolute -right-12 top-1/2 hidden -translate-y-1/2 text-ink/70 transition hover:text-ink md:block"
          >
            <ChevronRight className="h-8 w-8" />
          </button>
        </>
      )}

      {kontrole && (
        <div className="mt-2 flex justify-center gap-3">
          {Array.from({ length: ukupnoStrana }).map((_, i) => (
            <button
              key={i}
              onClick={() => naStranu(i)}
              aria-label={`Strana ${i + 1}`}
              className={cn(
                "h-2.5 rounded-full transition-all",
                i === strana ? "w-4 bg-honey-500" : "w-2.5 bg-ink/70 hover:bg-ink",
              )}
            />
          ))}
        </div>
      )}
    </div>
  );
}
