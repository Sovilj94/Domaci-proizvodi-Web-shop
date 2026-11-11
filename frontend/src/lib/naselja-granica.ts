/**
 * Granica naselja kako je čuva backend: GeoJSON `Polygon` ili `MultiPolygon`, koordinate (lon, lat).
 * Admin uređuje prvi (spoljni) prsten prvog dijela; ostali dijelovi i rupe ostaju kakvi jesu.
 * Mapa radi sa [lat, lon], pa se ovdje prevodi u oba smjera.
 */

/** Dijelovi granice: dio → prstenovi → tačke [lon, lat]. */
export type Granica = number[][][][];

export type Tacka = [number, number]; // [lat, lon]

export function procitajGranicu(geoJson: string | null | undefined): Granica {
  if (!geoJson) return [];
  try {
    const g = JSON.parse(geoJson) as { type: string; coordinates: unknown };
    if (g.type === "Polygon") return [g.coordinates as number[][][]];
    if (g.type === "MultiPolygon") return g.coordinates as number[][][][];
  } catch {
    // neispravan zapis se ponaša kao prazna granica — admin je crta iznova
  }
  return [];
}

/** Spoljni prsten prvog dijela kao [lat, lon], bez ponovljene zatvarajuće tačke. */
export function prviPrsten(g: Granica): Tacka[] {
  const prsten = g[0]?.[0] ?? [];
  const otvoren = prsten.length > 1 && jednake(prsten[0], prsten[prsten.length - 1]) ? prsten.slice(0, -1) : prsten;
  return otvoren.map(([lon, lat]) => [lat, lon]);
}

export function zamijeniPrviPrsten(g: Granica, prsten: Tacka[]): Granica {
  const lonLat = prsten.map(([lat, lon]) => [lon, lat]);
  const prvi = [lonLat, ...(g[0]?.slice(1) ?? [])];
  return [prvi, ...g.slice(1)];
}

/** GeoJSON za backend, sa zatvorenim prstenovima; null kad prvi prsten nema bar 3 tačke. */
export function zapisiGranicu(g: Granica): string | null {
  if (!g.length || (g[0][0]?.length ?? 0) < 3) return null;
  const zatvori = (r: number[][]) => (r.length && !jednake(r[0], r[r.length - 1]) ? [...r, r[0]] : r);
  const dijelovi = g.map((dio) => dio.map(zatvori));
  return JSON.stringify(
    dijelovi.length === 1
      ? { type: "Polygon", coordinates: dijelovi[0] }
      : { type: "MultiPolygon", coordinates: dijelovi },
  );
}

/** Ubacuje tačku u najbližu ivicu zatvorenog prstena (i onu od posljednje do prve tačke). */
export function dodajNaIvicu(prsten: Tacka[], t: Tacka): Tacka[] {
  if (prsten.length < 2) return [...prsten, t];
  let najbolji = 0;
  let min = Infinity;
  for (let i = 0; i < prsten.length; i++) {
    const d = udaljenostOdDuzi(t, prsten[i], prsten[(i + 1) % prsten.length]);
    if (d < min) {
      min = d;
      najbolji = i;
    }
  }
  return [...prsten.slice(0, najbolji + 1), t, ...prsten.slice(najbolji + 1)];
}

function udaljenostOdDuzi(p: Tacka, a: Tacka, b: Tacka): number {
  const [px, py] = p;
  const [ax, ay] = a;
  const [bx, by] = b;
  const dx = bx - ax;
  const dy = by - ay;
  const d2 = dx * dx + dy * dy;
  const k = d2 === 0 ? 0 : Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / d2));
  return Math.hypot(px - (ax + k * dx), py - (ay + k * dy));
}

function jednake(a: number[], b: number[]) {
  return a[0] === b[0] && a[1] === b[1];
}
