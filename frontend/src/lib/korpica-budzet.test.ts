import { test } from "node:test";
import assert from "node:assert/strict";
import { povecaj, procenat, smanji, staje, stanjeBudzeta, vrijednostNacrta, type StavkaNacrta } from "./korpica-budzet.ts";

const krompir: StavkaNacrta = { productId: "k", kolicina: 2, cijena: 2, najmanje: 1, korak: 0.5 };
const luk: StavkaNacrta = { productId: "l", kolicina: 0.3, cijena: 12, najmanje: 0.3, korak: 0.3 };

test("vrijednost nacrta je zbir količina × cijena, zaokružen na feninge", () => {
  assert.equal(vrijednostNacrta([krompir, luk]), 7.6);
});

test("plus dodaje jedan korak, bez grešaka zaokruživanja", () => {
  assert.equal(povecaj(krompir).kolicina, 2.5);
  assert.equal(povecaj({ ...luk, kolicina: 0.3 }).kolicina, 0.6);
});

test("minus oduzima korak, ali ne ispod najmanje — tada izbacuje (null)", () => {
  assert.equal(smanji(krompir)?.kolicina, 1.5);
  assert.equal(smanji({ ...krompir, kolicina: 1.4 }), null);
  assert.equal(smanji(luk), null);
});

test("procenat je udio cilja, cijeli broj", () => {
  assert.equal(procenat(6.44, 7), 92);
  assert.equal(procenat(5, 0), 0);
});

test("stanje budžeta: ispod 95 % malo, 95–100 % ok, iznad previše", () => {
  assert.equal(stanjeBudzeta(6.64, 7), "malo");
  assert.equal(stanjeBudzeta(6.65, 7), "ok");
  assert.equal(stanjeBudzeta(7, 7), "ok");
  assert.equal(stanjeBudzeta(7.01, 7), "previse");
});

test("plus iz količine ispod najmanje (stara korpica) skače na najmanju", () => {
  assert.equal(povecaj({ productId: "b", kolicina: 0.3, cijena: 5, najmanje: 0.5, korak: 0.5 }).kolicina, 0.5);
});

test("staje: dodatak smije dovesti korpicu tačno do 100 %, ne preko", () => {
  assert.equal(staje(6.5, 0.5, 7), true);
  assert.equal(staje(6.5, 0.51, 7), false);
  assert.equal(staje(7, 0, 7), true);
});
