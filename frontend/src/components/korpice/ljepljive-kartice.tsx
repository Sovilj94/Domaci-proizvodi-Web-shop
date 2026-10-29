"use client";

import Link from "next/link";
import { useEffect, useState, type RefObject } from "react";
import { buttonVariants } from "@/components/ui/button";
import { cijenaPlana, VELICINE, type KljucPlana } from "@/lib/planovi";
import { cn, formatPrice } from "@/lib/utils";
import type { Korpica, PlanPretplate } from "@/types/domain";

/**
 * Ljepljiva traka sa korpicama (po uzoru na farmit): kad kartice odu iznad ekrana, na vrhu ostaju
 * naziv, cijena izabranog plana i „Odaberi korpicu", pa se izbor može napraviti iz bilo koje sekcije.
 */
export function LjepljiveKartice({
  korpice,
  plan,
  kljucPlana,
  sidro,
}: {
  korpice: Korpica[];
  plan: PlanPretplate;
  kljucPlana: KljucPlana;
  sidro: RefObject<HTMLElement | null>;
}) {
  const [vidljiva, setVidljiva] = useState(false);

  useEffect(() => {
    const el = sidro.current;
    if (!el) return;
    const posmatrac = new IntersectionObserver(
      ([unos]) => setVidljiva(!unos.isIntersecting && unos.boundingClientRect.top < 0),
    );
    posmatrac.observe(el);
    return () => posmatrac.disconnect();
  }, [sidro]);

  if (korpice.length === 0) return null;

  return (
    <div
      aria-hidden={!vidljiva}
      className={cn(
        "fixed inset-x-0 top-0 z-[45] px-2 transition-transform duration-200 sm:px-4",
        vidljiva ? "translate-y-0" : "pointer-events-none -translate-y-full",
      )}
    >
      <div className="mx-auto grid max-w-[940px] grid-cols-3 gap-1.5 sm:gap-3">
        {korpice.map((k) => {
          const istaknuta = k.velicina === "srednja";
          const vel = VELICINE.find((v) => v.velicina === k.velicina);
          return (
            <div
              key={k.id}
              className="rounded-b-2xl bg-white px-2 pb-2 pt-2 shadow-[0_6px_16px_rgba(64,74,61,0.18)] sm:px-5 sm:pb-3"
            >
              <p className="truncate text-xs font-semibold text-ink sm:text-sm">{vel?.naziv ?? k.naziv} korpica</p>
              <p className="text-sm font-semibold text-ink sm:text-lg">
                {formatPrice(cijenaPlana(k, plan))}{" "}
                <span className="hidden text-[11px] font-normal text-muted sm:inline">/korpica</span>
              </p>
              <Link
                href={`/korpice/${k.tip}/${k.velicina}?plan=${kljucPlana}`}
                tabIndex={vidljiva ? undefined : -1}
                className={cn(
                  buttonVariants({ variant: istaknuta ? "istaknuto" : "primary", size: "sm" }),
                  "mt-1.5 w-full px-1 text-xs sm:text-sm",
                )}
              >
                <span>
                  Odaberi<span className="hidden sm:inline"> korpicu</span>
                </span>
              </Link>
            </div>
          );
        })}
      </div>
    </div>
  );
}
