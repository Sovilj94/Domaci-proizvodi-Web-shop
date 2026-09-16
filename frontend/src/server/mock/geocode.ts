import type { GeocodeHit } from "@/types/domain";

/*
 * Nominatim čitanje za mock endpointe. Oblik odgovora je isti koji pravi backend
 * vraća (GeocodeHitDto), da picker ne zna razliku između dvije implementacije.
 */

interface NominatimAddress {
  quarter?: string;
  neighbourhood?: string;
  suburb?: string;
  city_district?: string;
}

interface NominatimHit {
  lat?: string;
  lon?: string;
  display_name?: string;
  address?: NominatimAddress;
  error?: unknown;
}

/** Isti redoslijed kandidata kao na backendu, da pogađanje zone ostane isto. */
function localities(a?: NominatimAddress): string[] {
  if (!a) return [];
  return [a.quarter, a.neighbourhood, a.suburb, a.city_district].filter(
    (x): x is string => !!x,
  );
}

export async function nominatim(
  path: string,
  prviIzNiza: boolean,
): Promise<GeocodeHit | null> {
  try {
    const res = await fetch(`https://nominatim.openstreetmap.org/${path}`, {
      headers: {
        Accept: "application/json",
        // Nominatim odbija zahtjeve bez User-Agenta koji identifikuje aplikaciju.
        "User-Agent": "EPiljara/1.0 (mock, dostava Banja Luka)",
      },
    });
    if (!res.ok) return null;

    const data = (await res.json()) as NominatimHit[] | NominatimHit;
    const hit = prviIzNiza ? (Array.isArray(data) ? data[0] : undefined) : (data as NominatimHit);
    if (!hit || hit.error || !hit.lat || !hit.lon || !hit.display_name) return null;

    return {
      latitude: Number(hit.lat),
      longitude: Number(hit.lon),
      displayName: hit.display_name,
      localities: localities(hit.address),
    };
  } catch {
    return null;
  }
}
