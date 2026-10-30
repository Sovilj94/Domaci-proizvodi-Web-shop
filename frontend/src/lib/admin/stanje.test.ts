import { test } from "node:test";
import assert from "node:assert/strict";
import type { Korpica, Product } from "@/types/domain";
import { stanje } from "./stanje.ts";

const k = (naziv: string, stock: number, uProdaji: boolean, uskoGrlo?: string) =>
  ({ id: naziv, naziv, stock, uProdaji, dostupna: uProdaji && stock > 0, uskoGrlo }) as Korpica;
const p = (name: string, stock: number, unit: Product["unit"] = "kg", isAvailable = true) =>
  ({ id: name, name, stock, unit, isAvailable }) as Product;

test("korpice: one van ponude se ne prikazuju, rasprodate ostaju, sa artiklom koji prvi ponestane", () => {
  const s = stanje([k("Mala korpica povrća", 17, true, "Kelj"), k("Mala mliječna korpica", 0, false), k("Velika korpica voća", 0, true, "Dunja")], []);
  assert.deepEqual(s.korpice, [
    { id: "Mala korpica povrća", naziv: "Mala korpica povrća", ostalo: 17, jedinica: "kom", oznaka: "ok", uskoGrlo: "Kelj" },
    { id: "Velika korpica voća", naziv: "Velika korpica voća", ostalo: 0, jedinica: "kom", oznaka: "rasprodato", uskoGrlo: "Dunja" },
  ]);
});

test("korpice zadržavaju serverov redoslijed (veličina, ne abeceda)", () => {
  const s = stanje([k("Mala korpica voća", 1, true), k("Srednja korpica voća", 1, true), k("Velika korpica voća", 1, true), k("Mala korpica povrća", 1, true)], []);
  assert.deepEqual(s.korpice.map((r) => r.naziv), ["Mala korpica voća", "Srednja korpica voća", "Velika korpica voća", "Mala korpica povrća"]);
});

test("špajz: rasprodato i skinuto iz prodaje", () => {
  const s = stanje([], [p("Kajmak", 25), p("Jaja", 0, "kom"), p("Domaći ajvar", 45, "kom", false)]);
  assert.deepEqual(
    s.spajz.map((r) => [r.naziv, r.ostalo, r.jedinica, r.oznaka]),
    [
      ["Domaći ajvar", 45, "kom", "vanProdaje"],
      ["Jaja", 0, "kom", "rasprodato"],
      ["Kajmak", 25, "kg", "ok"],
    ],
  );
});

test("špajz po latiničnom abecednom redu (S prije Š), količine na dvije decimale", () => {
  const s = stanje([], [p("Šljiva", 1), p("Svježe mlijeko", 12.345, "l")]);
  assert.deepEqual(s.spajz.map((r) => r.naziv), ["Svježe mlijeko", "Šljiva"]);
  assert.equal(s.spajz[0].ostalo, 12.35);
});
