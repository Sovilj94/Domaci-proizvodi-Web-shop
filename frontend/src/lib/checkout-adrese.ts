import type { AddressInput } from "@/lib/api/contracts";
import type { Address } from "@/types/domain";

/** Isto kao `CreateAddressHandler.MaxAdresa` na serveru. */
export const MAX_ADRESA = 10;

const kljuc = (s: string) => s.trim().toLowerCase();

/**
 * Adresa koju checkout izabere sam. Adresa sa primaocem (roditelji…) se nikad ne bira sama —
 * kupac koji naručuje za sebe ne smije slučajno poslati korpicu nekom drugom.
 * Redoslijed: glavna iz adresara (ako je moja) → prva moja iz adresara → prva ranija adresa.
 */
export function podrazumijevanaAdresa(adresar: Address[], ranije: string[]): Address | string | null {
  const moje = adresar.filter((a) => !a.primalacIme);
  return moje.find((a) => a.isDefault) ?? moje[0] ?? ranije[0] ?? null;
}

/** Ranije adrese iz narudžbi koje nisu već u adresaru (poređenje bez razmaka i velikih slova). */
export function ranijeBezAdresara(adresar: Address[], ranije: string[]): string[] {
  const uAdresaru = new Set(adresar.map((a) => kljuc(a.fullAddress)));
  return ranije.filter((r) => !uAdresaru.has(kljuc(r)));
}

export type IshodCuvanja = "sacuvano" | "postoji" | "pun" | "greska";

/**
 * Pamti adresu „za nekog drugog" u adresar poslije uspješne narudžbe. Nikad ne baca —
 * narudžba je već napravljena i ništa ovdje je ne smije oboriti.
 * `dodaj` je `accountApi.dodajAdresu` (parametar, da bi se testiralo bez mreže).
 */
export async function sacuvajPrimaoca(
  postojece: Address[],
  unos: AddressInput,
  dodaj: (body: AddressInput) => Promise<Address>,
): Promise<IshodCuvanja> {
  if (postojece.some((a) => kljuc(a.fullAddress) === kljuc(unos.fullAddress))) return "postoji";
  if (postojece.length >= MAX_ADRESA) return "pun";
  try {
    await dodaj(unos);
    return "sacuvano";
  } catch {
    return "greska";
  }
}
