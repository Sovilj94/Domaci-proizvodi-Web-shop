import { test } from "node:test";
import assert from "node:assert/strict";
import type { Product } from "@/types/domain";
import {
  type KutijaRed,
  kolicinaZaDodavanje,
  kolicinaZaVrijednost,
  mjesecSedmice,
  naKorak,
  sezonskaPonuda,
  zamijeni,
  zbir,
} from "./kutija.ts";

const red = (r: Partial<KutijaRed>): KutijaRed => ({
  productId: "x", naziv: "X", jedinica: "kg", kolicina: 1, cijena: 1, nabavna: null, ...r,
});
const proizvod = (p: Partial<Product>) =>
  ({ id: "p", name: "P", price: 1, unit: "kg", stock: 10, isAvailable: true, mozeUKorpicu: true, mjeseci: [], ...p }) as Product;

test("koraci: komad je cio, kg na desetinu bez repova", () => {
  assert.equal(naKorak(0.30000000000000004, "kg"), 0.3);
  assert.equal(naKorak(2.6, "kom"), 3);
  assert.equal(kolicinaZaVrijednost(4, 1.2, "kg"), 3.3);
  assert.equal(kolicinaZaVrijednost(0.1, 5, "kom"), 1, "bar jedan komad");
});

test("zbir: vrijednost, nabavno i marža samo iz redova sa nabavnom", () => {
  const z = zbir([red({ kolicina: 2, cijena: 2, nabavna: 1 }), red({ kolicina: 1, cijena: 3 })], 10);
  assert.equal(z.vrijednost, 7);
  assert.equal(z.nabavno, 2);
  assert.equal(z.bezNabavne, 1);
  assert.equal(z.marza, 0.8);
  assert.equal(zbir([red({})], 0).marza, null);
});

test("dodavanje popuni ono što fali do cilja, a puna kutija dobije jedan korak", () => {
  assert.equal(kolicinaZaDodavanje(28.75, 24.75, 2, "kg"), 2);
  assert.equal(kolicinaZaDodavanje(28.75, 30, 2, "kg"), 0.1);
  assert.equal(kolicinaZaDodavanje(28.75, 25, 1.5, "kom"), 3);
});

test("zamjena zadržava vrijednost reda", () => {
  const novi = zamijeni(red({ kolicina: 2, cijena: 2 }), proizvod({ id: "t", name: "Tikvica", price: 1, nabavnaCijena: 0.6 }));
  assert.equal(novi.kolicina, 4);
  assert.equal(novi.nabavna, 0.6);
  assert.equal(novi.izmijenjeno, true);
});

test("sezonska ponuda: dostupno, za korpice, sa zalihom i u sezoni; mjesec sedmice po četvrtku", () => {
  const lista = sezonskaPonuda(
    [
      proizvod({ id: "a", name: "Malina", mjeseci: [6, 7] }),
      proizvod({ id: "b", name: "Krompir" }),
      proizvod({ id: "c", name: "Bez zalihe", stock: 0 }),
      proizvod({ id: "d", name: "Skriven", isAvailable: false }),
    ],
    11,
  );
  assert.deepEqual(lista.map((p) => p.id), ["b"]);
  assert.equal(mjesecSedmice(2026, 44), 10, "četvrtak 29.10.");
  assert.equal(mjesecSedmice(2026, 45), 11, "četvrtak 5.11.");
  assert.equal(mjesecSedmice(2027, 1), 1);
});
