"use client";

import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { SmartImage } from "@/components/ui/smart-image";
import { buttonVariants } from "@/components/ui/button";
import { cijenaPlana, obavezaPlana, VELICINE, type KljucPlana } from "@/lib/planovi";
import { cn, formatPrice } from "@/lib/utils";
import type { Korpica, PlanPretplate } from "@/types/domain";

/** Četiri reda sa emoji kao na farmit kartici — tvrde samo ono što aplikacija stvarno radi. */
export function OsobineKorpice({ obaveza }: { obaveza: number }) {
  return (
    <ul className="space-y-1.5 text-sm text-ink">
      <li>
        🚚 <strong>1 dostava</strong> sedmično do tvojih vrata
      </li>
      <li>
        🧺 <strong>Sezonske</strong> namirnice sa Manjače
      </li>
      <li>
        🧑‍🌾 <strong>Poznat domaćin</strong> za svaku namirnicu
      </li>
      <li>
        ✨ <strong>Izmijeni sadržaj</strong> nakon kupovine
      </li>
      {obaveza > 0 && (
        <li>
          📅 Obaveza: <strong>{obaveza} isporuka</strong>
        </li>
      )}
    </ul>
  );
}

/**
 * Kartica korpice (R2, po uzoru na farmit): na desktopu uspravna — fotografija, naziv,
 * „Korpica za 1 do 2 člana", osobine, cijena plana, „Odaberi korpicu" i link na sadržaj;
 * na mobilnom vodoravna sa zelenim okruglim dugmetom. Srednja je istaknuta (zeleno + „Popularno").
 */
export function KorpicaKartica({
  korpica,
  plan,
  kljucPlana,
  onSadrzaj,
}: {
  korpica: Korpica;
  plan: PlanPretplate;
  kljucPlana: KljucPlana;
  onSadrzaj: () => void;
}) {
  const vel = VELICINE.find((v) => v.velicina === korpica.velicina);
  const istaknuta = korpica.velicina === "srednja";
  const href = `/korpice/${korpica.tip}/${korpica.velicina}?plan=${kljucPlana}`;
  const cijena = cijenaPlana(korpica, plan);

  return (
    <>
      {/* Desktop */}
      <div className="relative hidden w-[310px] flex-col overflow-visible rounded-[26px] bg-white shadow-kartica transition-transform duration-150 hover:scale-[1.02] lg:flex">
        {istaknuta && (
          <span className="absolute -top-3 right-5 z-10 rounded-full bg-brand-600 px-3 py-1 text-xs font-medium text-white">
            Popularno
          </span>
        )}
        <Link href={href} className="relative block aspect-[4/3] overflow-hidden rounded-t-[26px] bg-sand">
          <SmartImage src={korpica.imageUrl} alt={korpica.naziv} sizes="310px" />
        </Link>
        <div className="flex flex-1 flex-col px-5 pb-5 pt-4">
          <h3 className="text-[22px] font-semibold text-ink">{vel?.naziv ?? korpica.naziv} korpica</h3>
          <p className="text-xs text-ink/80">Korpica {vel?.clanovi}</p>
          <div className="mt-3">
            <OsobineKorpice obaveza={obavezaPlana(korpica, plan)} />
          </div>
          <p className="mt-4 text-2xl font-semibold text-ink">
            {formatPrice(cijena)} <span className="text-sm font-normal text-muted">/korpica</span>
          </p>
          <Link
            href={href}
            className={cn(buttonVariants({ variant: istaknuta ? "istaknuto" : "primary", size: "lg" }), "mt-3 w-full")}
          >
            Odaberi korpicu
          </Link>
          <button
            type="button"
            onClick={onSadrzaj}
            className={cn(
              "mt-2 text-sm underline underline-offset-2",
              istaknuta ? "text-brand-600" : "text-honey-500",
            )}
          >
            Pogledaj sadržaj korpice
          </button>
        </div>
      </div>

      {/* Mobilni */}
      <div className="flex items-center gap-3 rounded-2xl bg-white p-3 shadow-[0_4px_16px_rgba(64,74,61,0.18)] lg:hidden">
        <Link href={href} className="relative h-24 w-24 shrink-0 overflow-hidden rounded-xl bg-sand">
          <SmartImage src={korpica.imageUrl} alt={korpica.naziv} sizes="96px" />
        </Link>
        <div className="min-w-0 flex-1">
          <h3 className="text-lg font-semibold text-ink">{vel?.naziv ?? korpica.naziv} korpica</h3>
          <p className="text-xs text-ink/80">Korpica {vel?.clanovi}</p>
          <p className="mt-2 text-base font-semibold text-ink">
            {formatPrice(cijena)} <span className="text-xs font-normal text-muted">/korpica</span>
          </p>
          <button type="button" onClick={onSadrzaj} className="text-sm text-brand-600 underline underline-offset-2">
            Sadržaj korpice
          </button>
        </div>
        <Link
          href={href}
          aria-label={`Odaberi ${korpica.naziv}`}
          className="grid h-10 w-10 shrink-0 place-items-center self-end rounded-full bg-brand-600 text-white"
        >
          <ArrowRight className="h-5 w-5" />
        </Link>
      </div>
    </>
  );
}
