import { test } from "node:test";
import assert from "node:assert/strict";
import type { StavkaPakerske } from "@/lib/api/contracts";
import { grupeNabavke, poGazdinstvu, spisakRobe } from "./nabavka.ts";

const s = (artikal: string, proizvodjac: string, kolicina: number, nabavnaCijena: number | null = null, jedinica = "kg") =>
  ({
    artikal,
    proizvodjac,
    selo: "Selo",
    kolicina,
    uKorpicama: kolicina,
    uSpajzu: 0,
    naStanju: 0,
    fali: 0,
    jedinica,
    artikalObrisan: false,
    nabavnaCijena,
  }) as StavkaPakerske;

test("jedan red po artiklu, bez obzira na proizvođača, po nazivu", () => {
  const r = spisakRobe([s("Paprika", "Kovačević", 10, 2.2), s("Luk", "Marić", 2), s("Paprika", "Jovanović", 1.5, 2)]);
  assert.deepEqual(
    r.map((x) => [x.artikal, x.kolicina, x.trosak]),
    [
      ["Luk", 2, null],
      ["Paprika", 11.5, 25],
    ],
  );
});

test("ista roba u drugoj jedinici ostaje poseban red", () => {
  const r = spisakRobe([s("Jaja", "A", 30, null, "kom"), s("Jaja", "B", 2, null, "kg")]);
  assert.deepEqual(r.map((x) => [x.jedinica, x.kolicina]).sort(), [
    ["kg", 2],
    ["kom", 30],
  ]);
});

test("grupe po vrsti robe: redom servera, Špajz na kraju, artikli po nazivu", () => {
  const g = (artikal: string, grupa: string, redGrupe: number, kolicina = 1, cijena: number | null = null) =>
    ({ ...s(artikal, "A", kolicina, cijena), kategorija: grupa === "Špajz" ? "Ajvar" : grupa, grupa, redGrupe }) as StavkaPakerske;
  const grupe = grupeNabavke(
    spisakRobe([
      g("Ajvar", "Špajz", 2147483647, 3, 8),
      g("Jabuka", "Voće", 1, 5, 1),
      g("Paprika", "Povrće", 0, 2, 2),
      g("Krompir", "Povrće", 0, 4),
      g("Džem", "Špajz", 2147483647, 1, 4),
    ]),
  );
  assert.deepEqual(
    grupe.map((x) => [x.naziv, x.redovi.map((r) => r.artikal), x.trosak]),
    [
      ["Povrće", ["Krompir", "Paprika"], 4],
      ["Voće", ["Jabuka"], 5],
      ["Špajz", ["Ajvar", "Džem"], 28],
    ],
  );
});

test("bez grupe sa servera (stariji API) sve ide u jednu grupu „Ostalo\"", () => {
  const grupe = grupeNabavke(spisakRobe([s("Luk", "A", 1), s("Jaja", "B", 2, null, "kom")]));
  assert.deepEqual(
    grupe.map((g) => [g.naziv, g.redovi.length]),
    [["Ostalo", 2]],
  );
});

const g = (artikal: string, kolicina: number, proizvodjacId?: string, proizvodjac = "", nabavnaCijena?: number) =>
  ({ ...s(artikal, proizvodjac, kolicina, nabavnaCijena ?? null), productId: artikal, proizvodjacId }) as StavkaPakerske;

test("poGazdinstvu: artikli istog gazdinstva u jednoj grupi, sa zbirom troška", () => {
  const grupe = poGazdinstvu([
    g("Krompir", 5, "p1", "Petković", 1),
    g("Luk", 2, "p1", "Petković", 2),
    g("Jabuka", 3, "p2", "Đurić", 1.5),
  ]);
  assert.deepEqual(grupe.map((x) => x.naziv), ["Đurić", "Petković"]);
  const petkovic = grupe.find((x) => x.proizvodjacId === "p1")!;
  assert.deepEqual(petkovic.redovi.map((r) => r.artikal), ["Krompir", "Luk"]);
  assert.equal(petkovic.trosak, 9);
});

test("poGazdinstvu: stavka bez gazdinstva ide u „Bez gazdinstva\" na kraju", () => {
  const grupe = poGazdinstvu([g("Nepoznato", 1), g("Krompir", 1, "p1", "Ažić")]);
  assert.deepEqual(grupe.map((x) => x.naziv), ["Ažić", "Bez gazdinstva"]);
  assert.equal(grupe[1].trosak, null);
});
