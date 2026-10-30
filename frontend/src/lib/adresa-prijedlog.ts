/**
 * Kako pogodak geokodera (prijedlog ili tačka sa mape) postaje tekst u polju adrese.
 * Izdvojeno iz `AddressPicker`-a da bi se moglo testirati bez pregledača.
 */

/** Dio `GeocodeHit`-a koji ovdje treba — lokalno, da test ne vuče `@/` putanje. */
export interface PogodakAdrese {
  displayName: string;
  street?: string | null;
  houseNumber?: string | null;
  city?: string | null;
}

/**
 * Tekst za polje i kućni broj za zasebno polje (ako ga ekran ima).
 *
 * - `odvojenBroj` (checkout): polje je samo ulica, broj ide u svoje polje. Broj se vraća
 *   samo kad ga pogodak zaista ima — ulica bez broja ne smije obrisati broj koji je kupac
 *   već upisao.
 * - inače (adresar, admin): cijela kratka adresa „Ulica broj, Grad".
 *
 * Kad geokoder ne zna ulicu (npr. pin u parku), ostaje njegov puni opis.
 */
export function tekstIzPogotka(
  hit: PogodakAdrese,
  odvojenBroj: boolean,
): { tekst: string; broj: string | null } {
  const ulica = hit.street?.trim();
  const broj = hit.houseNumber?.trim() || null;
  if (!ulica) return { tekst: hit.displayName, broj: null };
  if (odvojenBroj) return { tekst: ulica, broj };
  const prvi = broj ? `${ulica} ${broj}` : ulica;
  const grad = hit.city?.trim();
  return { tekst: grad ? `${prvi}, ${grad}` : prvi, broj: null };
}

/** Prvi red prijedloga u listi (podebljan) i drugi (sitno, sivo). */
export function redoviPrijedloga(hit: PogodakAdrese): { naslov: string; podnaslov: string } {
  const ulica = hit.street?.trim();
  if (!ulica) return { naslov: hit.displayName, podnaslov: "" };
  const broj = hit.houseNumber?.trim();
  const ulicaIBroj = broj ? `${ulica} ${broj}` : ulica;
  // Objekat (pekara, škola…) nosi svoje ime ispred adrese u `displayName` — neka ga i lista pokaže.
  const ime = hit.displayName.startsWith(ulicaIBroj) ? null : hit.displayName.split(",")[0]?.trim();
  return {
    naslov: ime ? `${ime} · ${ulicaIBroj}` : ulicaIBroj,
    podnaslov: hit.city?.trim() ?? "",
  };
}
