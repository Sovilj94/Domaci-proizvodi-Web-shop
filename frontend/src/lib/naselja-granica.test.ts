import { test } from "node:test";
import assert from "node:assert/strict";
import { dodajNaIvicu, procitajGranicu, prviPrsten, zapisiGranicu, zamijeniPrviPrsten } from "./naselja-granica.ts";

const kvadrat = JSON.stringify({
  type: "Polygon",
  coordinates: [[[17.2, 44.76], [17.21, 44.76], [17.21, 44.78], [17.2, 44.78], [17.2, 44.76]]],
});

test("granica: prvi prsten je [lat, lon] bez zatvarajuće tačke", () => {
  const g = procitajGranicu(kvadrat);
  assert.deepEqual(prviPrsten(g), [[44.76, 17.2], [44.76, 17.21], [44.78, 17.21], [44.78, 17.2]]);
});

test("granica: zapis zatvara prsten i vraća GeoJSON Polygon (lon, lat)", () => {
  const g = zamijeniPrviPrsten(procitajGranicu(null), [[44.0, 17.0], [44.0, 17.1], [44.1, 17.1]]);
  assert.deepEqual(JSON.parse(zapisiGranicu(g)!), {
    type: "Polygon",
    coordinates: [[[17.0, 44.0], [17.1, 44.0], [17.1, 44.1], [17.0, 44.0]]],
  });
});

test("granica: manje od 3 tačke nije granica", () => {
  const g = zamijeniPrviPrsten(procitajGranicu(null), [[44.0, 17.0], [44.0, 17.1]]);
  assert.equal(zapisiGranicu(g), null);
});

test("granica: multipoligon zadržava ostale dijelove", () => {
  const mp = JSON.stringify({
    type: "MultiPolygon",
    coordinates: [
      [[[17, 44], [17.1, 44], [17.1, 44.1], [17, 44]]],
      [[[18, 45], [18.1, 45], [18.1, 45.1], [18, 45]]],
    ],
  });
  const g = zamijeniPrviPrsten(procitajGranicu(mp), [[44, 17], [44, 17.2], [44.2, 17.2]]);
  const z = JSON.parse(zapisiGranicu(g)!);
  assert.equal(z.type, "MultiPolygon");
  assert.deepEqual(z.coordinates[1], [[[18, 45], [18.1, 45], [18.1, 45.1], [18, 45]]]);
});

test("ivica: nova tačka ide između krajeva najbliže ivice", () => {
  const prsten: [number, number][] = [[0, 0], [0, 10], [10, 10], [10, 0]];
  // Tačka blizu ivice [0,10]–[10,10] (gornja) ulazi između indeksa 1 i 2.
  assert.deepEqual(dodajNaIvicu(prsten, [5, 10.5]), [[0, 0], [0, 10], [5, 10.5], [10, 10], [10, 0]]);
  // Blizu zatvarajuće ivice [10,0]–[0,0] ide na kraj.
  assert.deepEqual(dodajNaIvicu(prsten, [5, -0.5]), [[0, 0], [0, 10], [10, 10], [10, 0], [5, -0.5]]);
});

test("ivica: prsten sa manje od 2 tačke samo dobija tačku na kraj", () => {
  assert.deepEqual(dodajNaIvicu([[1, 1]], [2, 2]), [[1, 1], [2, 2]]);
});
