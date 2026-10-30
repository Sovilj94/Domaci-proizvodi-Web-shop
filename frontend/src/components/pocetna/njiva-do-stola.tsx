"use client";

import { useBlokovi } from "@/lib/api/hooks";
import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";

/**
 * „Sa njive do tvog stola" (R1, po uzoru na farmit): obojene pločice-dugmad na krem pojasu.
 * Pločice su u bazi (sekcija „pocetna-plocice"); bez njih se sekcija ne prikazuje.
 */
export function NjivaDoStola() {
  const { blokovi: plocice } = useBlokovi("pocetna-plocice");
  if (plocice.length === 0) return null;
  return (
    <Sekcija ton="krem">
      <SectionHeader
        naslov="Sa njive do tvog stola"
        opis="Pogledaj naše korpice povrća i voća i izaberi šta ti treba iz Špajza."
      />
      <div className="mx-auto grid max-w-3xl gap-4 sm:grid-cols-3">
        {plocice.map((p) => (
          <Link
            key={p.id}
            href={p.link ?? "/"}
            className="flex h-[72px] items-center gap-3 rounded-2xl px-5 text-xl font-semibold shadow-[0_6px_16px_rgba(64,74,61,0.18)] transition hover:scale-[1.02]"
            style={{ backgroundColor: p.boja, color: p.bojaTeksta }}
          >
            <span className="text-3xl" aria-hidden>
              {p.ikona}
            </span>
            <span className="flex-1">{p.naslov}</span>
            <ChevronRight className="h-6 w-6" />
          </Link>
        ))}
      </div>
    </Sekcija>
  );
}
