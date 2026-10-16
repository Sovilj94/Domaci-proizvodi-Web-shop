import Link from "next/link";
import { PLANOVI, VRSTE, type KljucPlana } from "@/lib/planovi";
import { cn } from "@/lib/utils";

/**
 * Izbor vrste korpice (R2, po uzoru na farmit „🥦 povrće · 🍑 voće · 🥚 jaja"): okvir sa
 * tabovima, aktivni zeleno popunjen. Link čuva izabrani plan.
 */
export function VrstaSegment({ aktivna, plan, putanja }: { aktivna: string; plan: KljucPlana; putanja?: (tip: string) => string }) {
  return (
    <div className="mx-auto grid w-full max-w-[400px] grid-cols-2 gap-1 rounded-xl border border-line p-1 sm:max-w-[620px]">
      {VRSTE.map((v) => {
        const da = v.tip === aktivna;
        return (
          <Link
            key={v.tip}
            href={`${putanja ? putanja(v.tip) : `/korpice/${v.tip}`}?plan=${plan}`}
            className={cn(
              "flex items-center justify-center gap-2 rounded-lg py-2.5 text-lg font-semibold transition sm:text-2xl",
              da ? "bg-brand-500/60 text-brand-800" : "text-honey-500 hover:bg-honey-100",
            )}
            aria-current={da ? "page" : undefined}
          >
            <span aria-hidden>{v.emoji}</span> {v.naziv}
          </Link>
        );
      })}
    </div>
  );
}

/** Izbor plana (R2, po uzoru na farmit NEDELJNA · MESEČNA · 3 MESECA): zelena pilula. */
export function PlanSegment({ aktivan, href }: { aktivan: KljucPlana; href: (plan: KljucPlana) => string }) {
  return (
    <div className="mx-auto mt-4 flex w-fit rounded-full bg-brand-200 p-1">
      {PLANOVI.map((p) => {
        const da = p.kljuc === aktivan;
        return (
          <Link
            key={p.kljuc}
            href={href(p.kljuc)}
            scroll={false}
            className={cn(
              "rounded-full px-4 py-1.5 text-xs font-medium uppercase tracking-[0.06em] transition sm:px-6 sm:text-sm",
              da ? "bg-brand-600 text-white" : "text-brand-600 hover:text-brand-800",
            )}
            aria-current={da ? "true" : undefined}
          >
            {p.oznaka}
          </Link>
        );
      })}
    </div>
  );
}
