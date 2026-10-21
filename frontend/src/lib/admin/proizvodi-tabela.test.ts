import { test } from "node:test";
import assert from "node:assert/strict";
import type { Product } from "@/types/domain";
import { odgovaraPretrazi, prebaciMjesec, primijeni, prolaziFilter, ulazIzProizvoda } from "./proizvodi-tabela.ts";

const malina: Product = {
  id: "1", name: "Malina", slug: "malina", price: 6, unit: "kg", stock: 0, isAvailable: true,
  categoryId: "c", categoryName: "Voće", categorySlug: "voce", producerId: "p",
  producer: { id: "p", name: "Gazdinstvo Jovanović" } as Product["producer"], description: "",
  mozeUKorpicu: true, prioritetUKorpici: 0, mjeseci: [6, 7, 8, 9], nabavnaCijena: null, marza: null,
};

test("brza izmjena šalje obavezna polja proizvoda i samo izmijenjeno preko njih", () => {
  const ulaz = ulazIzProizvoda(malina, { stock: 12 });
  assert.equal(ulaz.stock, 12);
  assert.equal(ulaz.name, "Malina");
  assert.deepEqual(ulaz.mjeseci, [6, 7, 8, 9]);
  assert.equal("boja" in ulaz, false, "boja izostaje da je backend ne dira");
  assert.equal("nabavnaCijena" in ulaz, false);
});

test("optimističan prikaz računa maržu iz nove nabavne, a brisanje je skida", () => {
  assert.equal(primijeni(malina, { nabavnaCijena: 4.5 }).marza, 0.25);
  const sa = { ...malina, nabavnaCijena: 3, marza: 0.5 };
  assert.equal(primijeni(sa, { ukloniNabavnuCijenu: true }).marza, null);
  assert.equal(primijeni(sa, { price: 4 }).marza, 0.25);
});

test("filteri: bez zalihe gleda sezonu, bez nabavne samo artikle za korpice", () => {
  assert.equal(prolaziFilter(malina, "bez-zalihe", 7), true);
  assert.equal(prolaziFilter(malina, "bez-zalihe", 11), false, "van sezone nije problem");
  assert.equal(prolaziFilter(malina, "bez-nabavne", 7), true);
  assert.equal(prolaziFilter({ ...malina, mozeUKorpicu: false }, "bez-nabavne", 7), false);
  assert.equal(prolaziFilter({ ...malina, mjeseci: [] }, "u-sezoni", 1), true);
});

test("pretraga ne mari za kvačice i traži i po proizvođaču", () => {
  assert.equal(odgovaraPretrazi(malina, "jovanovic"), true);
  assert.equal(odgovaraPretrazi(malina, "VOCE"), true);
  assert.equal(odgovaraPretrazi(malina, "kruška"), false);
});

test("mjeseci: klik gasi/pali, cijela godina je prazan niz, zadnji mjesec ostaje", () => {
  assert.deepEqual(prebaciMjesec([6, 7], 8), [6, 7, 8]);
  assert.deepEqual(prebaciMjesec([6, 7], 7), [6]);
  assert.deepEqual(prebaciMjesec([6], 6), [6]);
  assert.deepEqual(prebaciMjesec([], 1), [2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
  assert.deepEqual(prebaciMjesec([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], 12), []);
});
