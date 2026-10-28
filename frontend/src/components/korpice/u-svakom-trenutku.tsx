import Link from "next/link";
import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";

/**
 * „U svakom trenutku možeš da:" (po uzoru na farmit): šta kupac sam podešava u Mom nalogu.
 * Svaka stavka postoji u aplikaciji; otkaz sa naloga važi po isteku obaveze plana.
 */
const MOGUCNOSTI = [
  { emoji: "🔄", naslov: "Paket", opis: "Zamijeniš korpicu prema svojim potrebama" },
  { emoji: "✏️", naslov: "Sadržaj", opis: "Zamijeniš namirnicu koju ne želiš" },
  { emoji: "⏸️", naslov: "Pauza", opis: "Pauziraš dostavu kad god želiš" },
  { emoji: "📍", naslov: "Dostava", opis: "Promijeniš adresu i termin dostave" },
  { emoji: "📞", naslov: "Podrška", opis: "Javiš nam se telefonom ili porukom" },
  { emoji: "❌", naslov: "Pretplata", opis: "Otkažeš pretplatu kad istekne obaveza plana" },
] as const;

export function USvakomTrenutku() {
  return (
    <Sekcija ton="krem">
      <SectionHeader
        naslov="U svakom trenutku možeš da:"
        opis="Iz svog naloga upravljaš pretplatom i korpicom jednim klikom."
      />
      <ul className="grid grid-cols-2 gap-x-4 gap-y-8 sm:grid-cols-3 lg:grid-cols-6">
        {MOGUCNOSTI.map((m) => (
          <li key={m.naslov} className="flex flex-col items-center text-center">
            <span className="grid h-16 w-16 place-items-center rounded-full bg-white text-3xl shadow-sm" aria-hidden>
              {m.emoji}
            </span>
            <h3 className="mt-3 text-xl font-semibold text-ink">{m.naslov}</h3>
            <p className="mt-1 max-w-[170px] text-sm text-ink/80">{m.opis}</p>
          </li>
        ))}
      </ul>
      <p className="mt-10 text-center">
        <Link href="/moj-nalog" className="text-sm font-medium text-brand-600 underline underline-offset-2">
          Otvori Moj nalog
        </Link>
      </p>
    </Sekcija>
  );
}
