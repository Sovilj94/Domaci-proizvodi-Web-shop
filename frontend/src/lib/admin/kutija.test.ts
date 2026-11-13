import { test } from "node:test";
import assert from "node:assert/strict";
import type { Product } from "@/types/domain";
import {
  type KutijaRed,
  ekonomicnost,
  granicaKutije,
  kolicinaZaDodavanje,
  kolicinaZaVrijednost,
  mjesecSedmice,
  najmanjaKolicina,
  najvecaKolicina,
  naKorak,
  sezonskaPonuda,
  slobodnoZaDodavanje,
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

test("zamjena po cijeni: 2 kg krompira (4 KM) → batat od 5 KM/kg = 0,8 kg, i kad batat ima zadatu količinu", () => {
  const batat = proizvod({ id: "b", name: "Batat", price: 5, unit: "kg", kolicinaMala: 0.7 });
  const n = zamijeni(red({ naziv: "Krompir", kolicina: 2, cijena: 2 }), batat, 0)!;
  assert.equal(n.kolicina, 0.8);
  assert.equal(n.kolicina * n.cijena, 4);
});

test("ekonomičnost: marža ispod minimuma javlja koliko je nabavno preko budžeta", () => {
  // Korpica 20 KM, minimum 25 % → nabavno smije najviše 15 KM; ovdje je 16 KM (marža 20 %).
  const redovi = [red({ productId: "a", kolicina: 2, cijena: 5, nabavna: 4 }), red({ productId: "b", kolicina: 4, cijena: 3, nabavna: 2 })];
  const e = ekonomicnost(redovi, { cijenaKorpice: 20, minimalnaMarza: 0.25 });
  assert.equal(e.uRedu, false);
  assert.deepEqual(e.marza, { marza: 0.2, minimum: 0.25, nabavno: 16, budzet: 15, preko: 1 });
});

test("ekonomičnost: marža tačno na minimumu je u redu, a bez nabavnih cijena se ne provjerava", () => {
  const naMinimumu = [red({ kolicina: 3, cijena: 6, nabavna: 5 })];
  assert.equal(ekonomicnost(naMinimumu, { cijenaKorpice: 20, minimalnaMarza: 0.25 }).uRedu, true);
  assert.equal(ekonomicnost([red({ kolicina: 9, cijena: 9 })], { cijenaKorpice: 20, minimalnaMarza: 0.25 }).uRedu, true);
  // Minimum još nije stigao sa servera: nema na šta da se upozori.
  assert.equal(ekonomicnost([red({ kolicina: 5, cijena: 5, nabavna: 5 })], { cijenaKorpice: 20 }).uRedu, true);
});

test("ekonomičnost: premalo kultura i vrijednost ispod donje granice", () => {
  const e = ekonomicnost([red({ kolicina: 2, cijena: 4 })], { cijenaKorpice: 20, minimalnoArtikala: 3, najmanjaVrijednost: 9.5 });
  assert.equal(e.faliArtikala, 2);
  assert.equal(e.faliVrijednosti, 1.5);
  assert.equal(e.uRedu, false);
  // Pola feninga tolerancije, kao na serveru.
  assert.equal(ekonomicnost([red({ kolicina: 1, cijena: 9.496 })], { cijenaKorpice: 20, najmanjaVrijednost: 9.5 }).faliVrijednosti, 0);
});

test("najmanja količina: pola zadate, a bez zadate pola vrijednosti jedne kulture", () => {
  assert.equal(najmanjaKolicina(1.5, 4, 2, "kg"), 0.7); // 0,75 naniže na korak
  assert.equal(najmanjaKolicina(0.3, 4, 12, "kg"), 0.1); // bijeli luk: nikad ispod koraka
  assert.equal(najmanjaKolicina(null, 4, 2, "kg"), 1); // 4 KM / 2 KM = 2 kg → 1 kg
  assert.equal(najmanjaKolicina(1, 4, 3, "kom"), 1); // komad je cio
  assert.equal(najmanjaKolicina(6, 4, 0.5, "kom"), 3);
});
