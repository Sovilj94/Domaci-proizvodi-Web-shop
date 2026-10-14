import type { Korpica, Sedmica } from "@/types/domain";
import { aktivniKrug, isporukaTekst, rokTekst } from "@/lib/sedmica";

/**
 * Brojevi koje odgovori smiju spomenuti. Svi dolaze sa servera (spec, O4) i svi su
 * opcioni: kad podatak ne stigne, odgovor ostaje tačan rečenicom bez broja.
 */
export interface KontekstPitanja {
  rok?: { tekst: string; zaSljedecu: boolean };
  isporuka?: string;
  popust?: { procenat: number; odIsporuke: number };
}

export interface Pitanje {
  pitanje: string;
  odgovor: (k: KontekstPitanja) => string;
}

export const CESTA_PITANJA: Pitanje[] = [
  {
    pitanje: "Šta je korpica?",
    odgovor: () =>
      "Pakovanje domaćih namirnica sa Manjače po fiksnoj cijeni. Vrstu i veličinu biraš ti, a sadržaj sastavljamo svake sedmice od onoga što je tada najbolje.",
  },
  {
    pitanje: "Do kada mogu naručiti?",
    odgovor: ({ rok }) =>
      !rok
        ? "Svaka sedmica ima rok naručivanja. Narudžba poslije roka ide za sljedeću sedmicu."
        : rok.zaSljedecu
          ? `Rok za ovu sedmicu je prošao. Za sljedeću sedmicu naručuješ do ${rok.tekst}.`
          : `Za ovu sedmicu naručuješ do ${rok.tekst}. Narudžba poslije roka ide za sljedeću sedmicu.`,
  },
  {
    pitanje: "Kada stiže dostava?",
    odgovor: ({ isporuka }) =>
      isporuka
        ? `Sljedeća isporuka je u ${isporuka} Dostavljamo u Banjoj Luci i okolini.`
        : "Dostavljamo jednom sedmično, u Banjoj Luci i okolini.",
  },
  {
    pitanje: "Kako se plaća?",
    odgovor: () => "Pouzećem, prilikom dostave. Ne tražimo karticu ni plaćanje unaprijed.",
  },
  {
    pitanje: "Mogu li promijeniti šta je u korpici?",
    odgovor: () =>
      "Možeš, do roka naručivanja, u okviru vrijednosti korpice. Izmjena je u Mom nalogu, pod „Moja korpica“. Cijena korpice se pri tome ne mijenja.",
  },
  {
    pitanje: "Kako radi pretplata?",
    odgovor: ({ popust }) =>
      "Uz pretplatu ti korpica stiže svake sedmice bez novog naručivanja. " +
      (popust
        ? `Od ${popust.odIsporuke}. isporuke korpica je ${popust.procenat}% jeftinija. `
        : "") +
      "Pauzu, promjenu paketa i otkaz podešavaš u Mom nalogu.",
  },
  {
    pitanje: "Treba li mi nalog?",
    odgovor: () =>
      "Za jednu narudžbu ne treba, možeš naručiti kao gost. Za pretplatu i izmjenu sadržaja korpice treba nalog.",
  },
];

/**
 * Sklapa kontekst iz sedmice i liste korpica. Popust se čita iz korpice čija je
 * `cijenaSaPretplatom` niža od pune (spec, O5) — klijent ne zna pravilo, samo ga čita.
 */
export function kontekstPitanja(sedmica?: Sedmica, korpice?: Korpica[]): KontekstPitanja {
  const k: KontekstPitanja = {};

  if (sedmica) {
    const krug = aktivniKrug(sedmica);
    k.rok = { tekst: rokTekst(krug.rok), zaSljedecu: krug.zaSljedecu };
    k.isporuka = isporukaTekst(krug.isporuka);
  }

  const sPopustom = korpice?.find(
    (x) =>
      x.cijenaSaPretplatom !== undefined &&
      x.isporukaDoPopusta !== undefined &&
      x.cijena > 0 &&
      x.cijenaSaPretplatom < x.cijena,
  );
  if (sPopustom) {
    const procenat = Math.round((1 - sPopustom.cijenaSaPretplatom! / sPopustom.cijena) * 100);
    // Popust ispod 1% bi dao „0% jeftinija" — tada odgovor ostaje bez broja.
    if (procenat >= 1) k.popust = { procenat, odIsporuke: sPopustom.isporukaDoPopusta! + 1 };
  }

  return k;
}
