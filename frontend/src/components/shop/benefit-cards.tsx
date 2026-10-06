import { Leaf, ShieldCheck, Sprout, Truck } from "lucide-react";

/**
 * Četiri benefita sa mockupa. Dijele ih početna i (u Fazi 2) detalj proizvoda, pa
 * tekstovi stoje na jednom mjestu umjesto da se prepisuju.
 *
 * Tvrdnje su namjerno one koje aplikacija stvarno drži: dostava je Banja Luka i
 * okolina (zone u bazi), proizvodi dolaze od proizvođača iz `producers`. Ništa se
 * ne obećava što se ne može potkrijepiti.
 */
const BENEFITI = [
  { ikona: Leaf, naslov: "100% svježe", opis: "Provjereni kvalitet" },
  { ikona: Sprout, naslov: "Domaći proizvodi", opis: "Podrška lokalnim proizvođačima" },
  { ikona: Truck, naslov: "Brza dostava", opis: "Banja Luka i okolina" },
  { ikona: ShieldCheck, naslov: "Sigurna kupovina", opis: "Vaši podaci su zaštićeni" },
] as const;

export function BenefitCards() {
  return (
    <ul className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {BENEFITI.map((b) => (
        <li
          key={b.naslov}
          className="flex items-center gap-3 rounded-card border border-line bg-surface p-3.5"
        >
          <span className="grid h-10 w-10 shrink-0 place-items-center rounded-[10px] bg-sand text-brand-600">
            <b.ikona className="h-5 w-5" />
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-ink">{b.naslov}</p>
            <p className="truncate text-xs text-muted">{b.opis}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}
