import { test } from "node:test";
import assert from "node:assert/strict";
import type { StavkaPakerske } from "@/lib/api/contracts";
import { poProizvodjacu } from "./nabavka.ts";

const s = (artikal: string, proizvodjac: string, kolicina: number, nabavnaCijena: number | null = null) =>
  ({ artikal, proizvodjac, selo: proizvodjac ? "Selo" : "", kolicina, nabavnaCijena }) as StavkaPakerske;

test("grupiše po proizvođaču, sabira nabavno i preskače artikle bez nabavne cijene", () => {
  const g = poProizvodjacu([s("Paprika", "Kovačević", 10, 2.2), s("Luk", "Kovačević", 2, 1.05), s("Med", "Jovanović", 1)]);
  assert.deepEqual(
    g.map((x) => [x.proizvodjac, x.stavke.map((y) => y.artikal), x.trosak]),
    [
      ["Jovanović", ["Med"], 0],
      ["Kovačević", ["Luk", "Paprika"], 24.1],
    ],
  );
});

test("artikli bez proizvođača idu na kraj", () => {
  const g = poProizvodjacu([s("Jaja", "", 30), s("Sir", "Marić", 1)]);
  assert.deepEqual(g.map((x) => x.proizvodjac), ["Marić", ""]);
});
