"use client";

import Link from "next/link";
import { Apple, Carrot, Ham, Milk, Package, type LucideIcon } from "lucide-react";
import { TIPOVI_KORPICE, type TipKorpice } from "@/types/domain";
import { cn } from "@/lib/utils";

// Exportovano radi ponovne upotrebe u admin ekranu sedmice (/admin/korpice), koji
// treba isti naziv tipa korpice bez dupliranja mape.
export const NAZIVI: Record<TipKorpice, { label: string; icon: LucideIcon }> = {
  povrce: { label: "Povrće", icon: Carrot },
  voce: { label: "Voće", icon: Apple },
  mlijecna: { label: "Mliječna", icon: Milk },
  suhomesnata: { label: "Suhomesnata", icon: Ham },
  domacinska: { label: "Domaćinska", icon: Package },
};

export function TipoviTabovi({ aktivan }: { aktivan: TipKorpice }) {
  return (
    <nav
      aria-label="Vrsta korpice"
      className="no-scrollbar mx-auto flex w-fit max-w-full gap-1 overflow-x-auto rounded-full border border-line bg-surface p-1"
    >
      {TIPOVI_KORPICE.map((tip) => {
        const { label, icon: Icon } = NAZIVI[tip];
        const active = tip === aktivan;
        return (
          <Link
            key={tip}
            href={`/korpice?tip=${tip}`}
            aria-current={active ? "page" : undefined}
            className={cn(
              "flex shrink-0 items-center gap-1.5 rounded-full px-4 py-2 text-sm font-medium transition",
              active ? "bg-brand-100 text-brand-700" : "text-muted hover:bg-sand",
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </Link>
        );
      })}
    </nav>
  );
}
