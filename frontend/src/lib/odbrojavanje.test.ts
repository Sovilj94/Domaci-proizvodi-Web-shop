import { test } from "node:test";
import assert from "node:assert/strict";
import { preostalo } from "./odbrojavanje.ts";

const sada = Date.parse("2026-11-26T10:00:00Z");
const za = (min: number) => new Date(sada + min * 60_000).toISOString();

test("preostalo: dani i sati kad je više od dana", () => {
  assert.equal(preostalo(za(4 * 1440 + 3 * 60 + 20), sada), "4 d 3 h");
});

test("preostalo: sati i minute ispod dana", () => {
  assert.equal(preostalo(za(5 * 60 + 7), sada), "5 h 7 min");
});

test("preostalo: samo minute ispod sata", () => {
  assert.equal(preostalo(za(42), sada), "42 min");
});

test("preostalo: prošao rok", () => {
  assert.equal(preostalo(za(-1), sada), null);
});
