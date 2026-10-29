import { test } from "node:test";
import assert from "node:assert/strict";
import { uEngleski } from "./prevod.ts";

test("tačan tekst iz rječnika, sa sačuvanim razmacima", () => {
  assert.equal(uEngleski("Izaberi korpicu"), "Choose your box");
  assert.equal(uEngleski(" korpica"), " box");
});

test("obrasci sa brojevima i oblicima riječi", () => {
  assert.equal(uEngleski("4 dostave"), "4 deliveries");
  assert.equal(uEngleski("1 dostava"), "1 delivery");
  assert.equal(uEngleski("/12 korpica"), "/12 boxes");
  assert.equal(uEngleski("Srednja korpica povrća"), "Medium vegetable box");
});

test("nepoznat tekst ostaje kakav jeste", () => {
  assert.equal(uEngleski("Porodica Kovačević"), "Porodica Kovačević");
});
