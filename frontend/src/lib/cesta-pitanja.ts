import type { Korpica, Sedmica } from "@/types/domain";
import { aktivniKrug, isporukaTekst, rokTekst } from "@/lib/sedmica";

/**
 * Brojevi koje odgovori smiju spomenuti. Svi dolaze sa servera (spec, O4) i svi su
 * opcioni: kad podatak ne stigne, odgovor ostaje tačan rečenicom bez broja.
 */
export interface KontekstPitanja {
  rok?: { tekst: string; zaSljedecu: boolean };
  isporuka?: string;
  popust?: { nedjeljna: number; triMjeseca: number };
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
        ? `Pretplatnička cijena važi od prve isporuke: ${popust.nedjeljna}% jeftinije uz nedjeljni plan, do ${popust.triMjeseca}% uz plan na 3 mjeseca. `
        : "") +
      "Mjesečni i tromjesečni plan nose obavezu na 4, odnosno 13 isporuka. Pauzu i promjenu paketa podešavaš u Mom nalogu.",
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

  // R2: popust po planu važi od prve isporuke; procenti se izvode iz cijena sa servera.
  const sCijenama = korpice?.find(
    (x) => x.cijena > 0 && x.cijenaNedjeljna !== undefined && x.cijenaTriMjeseca !== undefined,
  );
  if (sCijenama) {
    const p = (c: number) => Math.round((1 - c / sCijenama.cijena) * 100);
    const nedjeljna = p(sCijenama.cijenaNedjeljna!);
    const triMjeseca = p(sCijenama.cijenaTriMjeseca!);
    // Popust ispod 1% bi dao „0% jeftinija" — tada odgovor ostaje bez broja.
    if (nedjeljna >= 1) k.popust = { nedjeljna, triMjeseca };
  }

  return k;
}

/**
 * Česta pitanja kao TEKST — polazna verzija koju admin vidi u `/admin/sadrzaj` prije prve izmjene.
 * Isti sadržaj kao `CESTA_PITANJA`, samo su brojevi sa servera zamijenjeni oznakama.
 */
export const OZNAKE_PITANJA = ["{rok}", "{isporuka}", "{popustNedjeljna}", "{popustTriMjeseca}"] as const;

export const PODRAZUMIJEVANA_PITANJA: { pitanje: string; odgovor: string }[] = [
  {
    pitanje: "Šta je korpica?",
    odgovor:
      "Pakovanje domaćih namirnica sa Manjače po fiksnoj cijeni. Vrstu i veličinu biraš ti, a sadržaj sastavljamo svake sedmice od onoga što je tada najbolje.",
  },
  {
    pitanje: "Do kada mogu naručiti?",
    odgovor: "Naručuješ do {rok}. Narudžba poslije roka ide za sljedeću sedmicu.",
  },
  {
    pitanje: "Kada stiže dostava?",
    odgovor: "Sljedeća isporuka je u {isporuka} Dostavljamo u Banjoj Luci i okolini.",
  },
  {
    pitanje: "Kako se plaća?",
    odgovor: "Pouzećem, prilikom dostave. Ne tražimo karticu ni plaćanje unaprijed.",
  },
  {
    pitanje: "Mogu li promijeniti šta je u korpici?",
    odgovor:
      "Možeš, do roka naručivanja, u okviru vrijednosti korpice. Izmjena je u Mom nalogu, pod „Moja korpica“. Cijena korpice se pri tome ne mijenja.",
  },
  {
    pitanje: "Kako radi pretplata?",
    odgovor:
      "Uz pretplatu ti korpica stiže svake sedmice bez novog naručivanja. Pretplatnička cijena važi od prve isporuke: {popustNedjeljna}% jeftinije uz nedjeljni plan, do {popustTriMjeseca}% uz plan na 3 mjeseca. Mjesečni i tromjesečni plan nose obavezu na 4, odnosno 13 isporuka. Pauzu i promjenu paketa podešavaš u Mom nalogu.",
  },
  {
    pitanje: "Treba li mi nalog?",
    odgovor:
      "Za jednu narudžbu ne treba, možeš naručiti kao gost. Za pretplatu i izmjenu sadržaja korpice treba nalog.",
  },
];

/**
 * Popunjava oznake u odgovoru koji je admin unio. Kad podatak sa servera ne stigne, oznaka dobija
 * neutralnu zamjenu, da rečenica ostane čitljiva.
 */
export function popuniOdgovor(tekst: string, k: KontekstPitanja): string {
  return tekst
    .replaceAll("{rok}", k.rok?.tekst ?? "roka naručivanja")
    .replaceAll("{isporuka}", k.isporuka ?? "sljedećem terminu.")
    .replaceAll("{popustNedjeljna}", String(k.popust?.nedjeljna ?? 10))
    .replaceAll("{popustTriMjeseca}", String(k.popust?.triMjeseca ?? 20));
}
