import { test } from "node:test";
import assert from "node:assert/strict";
import type { StavkaPakerske } from "@/lib/api/contracts";
import { grupeNabavke, spisakRobe } from "./nabavka.ts";

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
