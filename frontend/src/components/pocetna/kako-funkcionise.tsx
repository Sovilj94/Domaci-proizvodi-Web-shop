import { PackageCheck, ShoppingBasket, Truck } from "lucide-react";
import { SectionHeader } from "@/components/ui/section-header";

/**
 * Tri koraka. Namjerno bez brojeva (rok, dan, cijena) — oni stoje u heroju i FAQ-u, iz
 * servera; ovdje bi bili treća kopija koja može da se razide (spec, O12).
 */
const KORACI = [
  {
    ikona: ShoppingBasket,
    naslov: "Izaberi korpicu",
    opis: "Povrće, voće, mliječna, suhomesnata ili domaćinska — u tri veličine.",
  },
  {
    ikona: PackageCheck,
    naslov: "Mi je spakujemo",
    opis: "Sadržaj sastavljamo svake sedmice od onoga što je na Manjači tada najbolje.",
  },
  {
    ikona: Truck,
    naslov: "Stiže na tvoja vrata",
    opis: "Vozač je donosi u dogovoreni termin, a plaćaš pouzećem.",
  },
] as const;

export function KakoFunkcionise() {
  return (
    <>
      <SectionHeader naslov="Kako funkcioniše" />
      <ol className="grid gap-3 md:grid-cols-3 lg:gap-4">
        {KORACI.map((k, i) => (
          <li key={k.naslov} className="rounded-card border border-line bg-surface p-5">
            <div className="flex items-center gap-3">
              <span className="grid h-10 w-10 shrink-0 place-items-center rounded-[10px] bg-sand text-brand-600">
                <k.ikona className="h-5 w-5" />
              </span>
              <span className="text-xs font-bold uppercase tracking-[0.14em] text-brand-700">
                Korak {i + 1}
              </span>
            </div>
            <h3 className="mt-3 text-base font-semibold text-ink">{k.naslov}</h3>
            <p className="mt-1 text-sm text-muted">{k.opis}</p>
          </li>
        ))}
      </ol>
    </>
  );
}
