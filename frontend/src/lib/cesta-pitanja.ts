import type { Korpica, Pitanje, Sedmica } from "@/types/domain";
import { aktivniKrug, isporukaTekst, rokTekst } from "./sedmica.ts";

/*
 * Česta pitanja su u bazi (SadrzajSajta.Pitanja, admin `/admin/sadrzaj`) — sajt nema svoja.
 * Odgovor smije nositi oznake koje se ovdje popunjavaju brojevima sa servera.
 */

/** Brojevi koje odgovori smiju spomenuti. Svi dolaze sa servera i svi su opcioni. */
export interface KontekstPitanja {
  rok?: { tekst: string; zaSljedecu: boolean };
  isporuka?: string;
  popust?: { nedjeljna: number; triMjeseca: number };
  obaveza?: { mjesecna: number; triMjeseca: number };
}

export const OZNAKE_PITANJA = [
  "{rok}",
  "{isporuka}",
  "{popustNedjeljna}",
  "{popustTriMjeseca}",
  "{obavezaMjesecna}",
  "{obavezaTriMjeseca}",
] as const;

/**
 * Sklapa kontekst iz sedmice i liste korpica. Popust se čita iz korpice čija je pretplatna
 * cijena niža od pune (spec, O5), a obaveza plana iz iste korpice — klijent ne zna pravilo,
 * samo ga čita.
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
    // Popust ispod 1% bi dao „0% jeftinija" — tada rečenica sa popustom otpada.
    if (nedjeljna >= 1) k.popust = { nedjeljna, triMjeseca };
  }

  const sObavezom = korpice?.find((x) => x.obavezaMjesecna !== undefined && x.obavezaTriMjeseca !== undefined);
  if (sObavezom) k.obaveza = { mjesecna: sObavezom.obavezaMjesecna!, triMjeseca: sObavezom.obavezaTriMjeseca! };

  return k;
}

/**
 * Popunjava oznake u odgovoru iz baze. Rečenica čija oznaka nema podatak sa servera se izostavlja —
 * bolje bez rečenice nego sa izmišljenim brojem.
 */
export function popuniOdgovor(tekst: string, k: KontekstPitanja): string {
  const vrijednosti: Record<string, string | undefined> = {
    "{rok}": k.rok?.tekst,
    "{isporuka}": k.isporuka,
    "{popustNedjeljna}": k.popust ? String(k.popust.nedjeljna) : undefined,
    "{popustTriMjeseca}": k.popust ? String(k.popust.triMjeseca) : undefined,
    "{obavezaMjesecna}": k.obaveza ? String(k.obaveza.mjesecna) : undefined,
    "{obavezaTriMjeseca}": k.obaveza ? String(k.obaveza.triMjeseca) : undefined,
  };
  const recenice = tekst.match(/[^.!?]+(?:[.!?]+|$)\s*/g) ?? [tekst];
  return recenice
    .filter((r) => OZNAKE_PITANJA.every((o) => !r.includes(o) || vrijednosti[o] !== undefined))
    .map((r) => OZNAKE_PITANJA.reduce((t, o) => t.replaceAll(o, vrijednosti[o] ?? ""), r))
    .join("")
    .trim();
}

/** Pitanje i odgovor na jeziku sajta: engleska verzija iz baze kad postoji, inače srpska. */
export function naJeziku(p: Pitanje, engleski: boolean): { pitanje: string; odgovor: string } {
  return engleski
    ? { pitanje: p.pitanjeEn || p.pitanje, odgovor: p.odgovorEn || p.odgovor }
    : { pitanje: p.pitanje, odgovor: p.odgovor };
}
