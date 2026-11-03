import { test } from "node:test";
import assert from "node:assert/strict";
import type { Product } from "@/types/domain";
import {
  type KutijaRed,
  granicaKutije,
  kolicinaZaNovi,
  kolicinaZaDodavanje,
  kolicinaZaVrijednost,
  mjesecSedmice,
  najvecaKolicina,
  naKorak,
  sezonskaPonuda,
  slobodnoZaDodavanje,
  zadataKolicina,
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

test("granica je cilj, a kutija koja je već preko smije samo da se smanjuje", () => {
  assert.equal(granicaKutije(28.75, 25), 28.75);
  assert.equal(granicaKutije(28.75, 30), 30);
  assert.equal(granicaKutije(0, 30), null);
  assert.equal(slobodnoZaDodavanje(28.75, 24.75), 4);
  assert.equal(slobodnoZaDodavanje(28.75, 30), 0);
  assert.equal(slobodnoZaDodavanje(null, 30), Infinity);
});

test("dodavanje stane u ono što je slobodno, zaokruženo naniže; puna kutija ne prima ništa", () => {
  assert.equal(kolicinaZaDodavanje(4, 2, "kg"), 2);
  assert.equal(kolicinaZaDodavanje(3.75, 1.5, "kom"), 2); // 3 kom bi bilo 4,50 KM — preko
  assert.equal(kolicinaZaDodavanje(0, 2, "kg"), 0);
  assert.equal(kolicinaZaDodavanje(1, 1.5, "kom"), 0);
  assert.equal(kolicinaZaDodavanje(Infinity, 2, "kg"), 0.1); // cilj nepoznat — jedan korak
});

test("količina reda ne raste preko slobodnog, a ono što već stoji ostaje", () => {
  assert.equal(najvecaKolicina(red({ kolicina: 2, cijena: 2 }), 1), 2.5);
  assert.equal(najvecaKolicina(red({ kolicina: 2, cijena: 2 }), 0), 2);
  assert.equal(najvecaKolicina(red({ kolicina: 2, cijena: 2 }), Infinity), Infinity);
});

test("zamjena zadržava vrijednost reda", () => {
  const novi = zamijeni(red({ kolicina: 2, cijena: 2 }), proizvod({ id: "t", name: "Tikvica", price: 1, nabavnaCijena: 0.6 }))!;
  assert.equal(novi.kolicina, 4);
  assert.equal(novi.nabavna, 0.6);
  assert.equal(novi.izmijenjeno, true);
});

test("zamjena ne probija granicu zaokruživanjem naviše", () => {
  // Red od 2,50 KM; jaja po 1,5 KM: zaokruženo bi bilo 2 kom = 3 KM.
  const jaja = proizvod({ id: "j", name: "Jaja", price: 1.5, unit: "kom" });
  assert.equal(zamijeni(red({ kolicina: 1, cijena: 2.5 }), jaja, 0)!.kolicina, 1);
  assert.equal(zamijeni(red({ kolicina: 1, cijena: 2.5 }), jaja, 0.5)!.kolicina, 2);
  // Red od 1 KM, artikal od 5 KM na komad, ništa slobodno — ne stane.
  assert.equal(zamijeni(red({ kolicina: 1, cijena: 1 }), proizvod({ id: "m", name: "Med", price: 5, unit: "kom" }), 0), null);
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

test("zadata količina za veličinu korpice ide cijela — ili nikako kad ne staje", () => {
  const luk = proizvod({ id: "l", name: "Luk", price: 2, unit: "kg", kolicinaMala: 1, kolicinaSrednja: 1.5, kolicinaVelika: 0 });
  assert.equal(zadataKolicina(luk, "mala"), 1);
  assert.equal(zadataKolicina(luk, "velika"), null, "0 znači nije zadato");
  assert.equal(zadataKolicina(luk, null), null);
  assert.equal(kolicinaZaNovi(luk, "srednja", 5), 1.5);
  assert.equal(kolicinaZaNovi(luk, "srednja", 2.5), 0, "1,5 kg = 3 KM ne staje u 2,50");
  assert.equal(kolicinaZaNovi(luk, "velika", 3), 1.5, "bez zadate — ono što stane");
});

test("zamjena stavlja zadatu količinu novog artikla, ne vrijednost starog", () => {
  const luk = proizvod({ id: "l", name: "Luk", price: 2, unit: "kg", kolicinaMala: 1 });
  // Krompir 3 kg × 2 KM = 6 KM van; luk ide 1 kg (2 KM), ne 3 kg.
  assert.equal(zamijeni(red({ kolicina: 3, cijena: 2 }), luk, 0, "mala")!.kolicina, 1);
  // Zadata količina skuplja od reda + slobodnog ne staje.
  assert.equal(zamijeni(red({ kolicina: 0.5, cijena: 2 }), luk, 0.5, "mala"), null);
});
