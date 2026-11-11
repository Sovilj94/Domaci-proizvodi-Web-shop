import { test } from "node:test";
import assert from "node:assert/strict";
import { cijenaDostave, imaKorpicu } from "./dostava.ts";

const pravila = { dostavaSaKorpicom: 0, dostavaBezKorpice: 5, minimalnaNarudzba: 0 };

test("dostava: uz korpicu je besplatna", () => {
  assert.equal(cijenaDostave(true, pravila), 0);
});

test("dostava: bez korpice 5 KM", () => {
  assert.equal(cijenaDostave(false, pravila), 5);
});

test("dostava: bez pravila (još se učitavaju) nema iznosa", () => {
  assert.equal(cijenaDostave(false, undefined), null);
});

test("korpica: prepoznaje se po id-u iz liste korpica", () => {
  const korpice = new Set(["k1"]);
  assert.equal(imaKorpicu(["p1", "k1"], korpice), true);
  assert.equal(imaKorpicu(["p1"], korpice), false);
  assert.equal(imaKorpicu([], korpice), false);
});
