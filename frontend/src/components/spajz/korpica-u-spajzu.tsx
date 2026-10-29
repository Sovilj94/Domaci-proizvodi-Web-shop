"use client";

import Link from "next/link";
import { SmartImage } from "@/components/ui/smart-image";
import { cijenaPlana, VELICINE, VRSTE } from "@/lib/planovi";
import { formatPrice } from "@/lib/utils";
import type { Korpica } from "@/types/domain";

/**
 * Korpica kao artikal Špajza (po uzoru na farmit, gdje su gajbice u kategoriji „Povrće"):
 * isti izgled kao ProductCard, ali vodi na stranicu korpice — tamo se bira plan i kupuje.
 * Cijena je nedjeljna pretplata („od"), kao najniža cijena jedne korpice bez obaveze.
 */
export function KorpicaUSpajzu({ korpica }: { korpica: Korpica }) {
  const href = `/korpice/${korpica.tip}/${korpica.velicina}`;
  const vel = VELICINE.find((v) => v.velicina === korpica.velicina);
  const vrsta = VRSTE.find((v) => v.tip === korpica.tip);

  return (
    <div className="group flex h-full flex-col overflow-hidden rounded-xl border border-line/70 bg-white">
      <Link href={href} className="relative m-1.5 block aspect-square overflow-hidden rounded-lg bg-sand">
        <SmartImage src={korpica.imageUrl} alt={korpica.naziv} className="transition duration-500 group-hover:scale-105" />
      </Link>
      <div className="flex flex-1 flex-col px-3 pb-3 pt-2">
        <Link href={href} className="min-h-[2.5rem] text-sm leading-snug text-ink transition hover:text-honey-500">
          {korpica.naziv}
        </Link>
        <p className="mt-0.5 truncate text-[11px] text-muted">Korpica {vel?.clanovi}</p>
        <div className="mt-auto flex items-baseline justify-between gap-2 pt-3">
          <span className="text-sm font-semibold text-ink">{formatPrice(cijenaPlana(korpica, "Nedjeljna"))}</span>
          <span className="text-sm text-ink">{vrsta?.emoji}</span>
        </div>
        <Link
          href={href}
          className="mt-2.5 inline-flex h-10 w-full items-center justify-center rounded-full bg-brand-600 text-sm font-semibold text-white transition hover:bg-brand-700"
        >
          Odaberi korpicu
        </Link>
      </div>
    </div>
  );
}
