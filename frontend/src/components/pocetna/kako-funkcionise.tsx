import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";

/**
 * Tri koraka (R1, po uzoru na farmit): bijeli krug sa emoji, isprekidana linija između,
 * naslov i tekst; na mobilnom vertikalno sa ikonom lijevo. Namjerno bez brojeva (rok, dan,
 * cijena) — oni dolaze sa servera na drugim mjestima (spec P4, O12).
 */
const KORACI = [
  {
    emoji: "📦",
    naslov: "Biraš korpicu",
    opis: "Izaberi korpicu povrća ili voća i veličinu po broju ukućana. Možeš se pretplatiti ili naručiti jednom.",
  },
  {
    emoji: "🧑‍🌾",
    naslov: "Znaš domaćina",
    opis: "Uz svaku namirnicu u korpici piše ko ju je uzgojio i iz kog je sela.",
  },
  {
    emoji: "🍅",
    naslov: "Stiže na vrata",
    opis: "Dostavljamo u dan koji izabereš, a plaćaš pouzećem. Adresu dostave možeš da mijenjaš.",
  },
] as const;

export function KakoFunkcionise() {
  return (
    <Sekcija ton="krem" luk>
      <SectionHeader naslov="Kako funkcioniše?" />
      <ol className="relative grid gap-8 md:grid-cols-3 md:gap-6">
        {/* Isprekidana linija iza krugova, samo na desktopu. */}
        <div
          className="absolute left-[16.6%] right-[16.6%] top-10 hidden border-t-[3px] border-dashed border-white md:block"
          aria-hidden
        />
        {KORACI.map((k) => (
          <li key={k.naslov} className="relative flex gap-5 md:flex-col md:items-center md:text-center">
            <span className="grid h-20 w-20 shrink-0 place-items-center rounded-full bg-white text-4xl shadow-sm" aria-hidden>
              {k.emoji}
            </span>
            <div>
              <h3 className="text-2xl font-semibold text-ink md:mt-4 md:text-[28px]">{k.naslov}</h3>
              <p className="mt-2 text-sm leading-relaxed text-ink/80 md:mx-auto md:max-w-[270px]">{k.opis}</p>
            </div>
          </li>
        ))}
      </ol>
    </Sekcija>
  );
}
