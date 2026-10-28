import { Sekcija } from "@/components/pocetna/sekcija";
import { SectionHeader } from "@/components/ui/section-header";

/**
 * „Šta je bitno da znaš?" (po uzoru na farmit): tri tvrdnje sa emoji, pa tri koraka do prve
 * korpice. Tvrdi samo ono što aplikacija radi — naručivanje i kao gost, plaćanje pouzećem.
 */
export function BitnoDaZnas({ vrsta }: { vrsta: "povrce" | "voce" }) {
  const sta = vrsta === "voce" ? "voća" : "povrća";
  const tvrdnje = [
    { emoji: "🧑‍🌾", tekst: `Svake sedmice na tvoju adresu stiže mješavina svježeg sezonskog ${sta} sa Manjače.` },
    { emoji: "🚚", tekst: "Izaberi veličinu koja odgovara tvom domaćinstvu, porodici i gostima." },
    { emoji: "🥦", tekst: "Poznati domaćini na svojim imanjima uzgajaju za tebe — uz svaku namirnicu piše ko i odakle." },
  ];
  const koraci = [
    { emoji: "🧺", naslov: "Odaberi korpicu", opis: `Uživaj u svježem i raznovrsnom sezonskom ${vrsta === "voce" ? "voću" : "povrću"}.` },
    { emoji: "👤", naslov: "Naruči", opis: "Prijavi se ili naruči kao gost, pa izaberi adresu i termin dostave." },
    { emoji: "💰", naslov: "Plaćanje", opis: "Plaćaš pouzećem, kad ti korpica stigne na vrata." },
  ];

  return (
    <Sekcija ton="krem" luk>
      <SectionHeader naslov="Šta je bitno da znaš?" />
      <ul className="mx-auto max-w-2xl space-y-3">
        {tvrdnje.map((t) => (
          <li key={t.emoji} className="flex gap-3 text-[15px] text-ink">
            <span aria-hidden>{t.emoji}</span>
            {t.tekst}
          </li>
        ))}
      </ul>
      <ol className="mt-12 grid gap-8 md:grid-cols-3 md:gap-6">
        {koraci.map((k) => (
          <li key={k.naslov} className="flex gap-5 md:flex-col md:items-center md:text-center">
            <span className="grid h-16 w-16 shrink-0 place-items-center rounded-full bg-white text-3xl shadow-sm" aria-hidden>
              {k.emoji}
            </span>
            <div>
              <h3 className="text-2xl font-semibold text-ink md:mt-3">{k.naslov}</h3>
              <p className="mt-1 text-sm leading-relaxed text-ink/80 md:mx-auto md:max-w-[260px]">{k.opis}</p>
            </div>
          </li>
        ))}
      </ol>
    </Sekcija>
  );
}
