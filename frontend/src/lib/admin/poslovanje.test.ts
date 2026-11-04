import { test } from "node:test";
import assert from "node:assert/strict";
import { procenat, promjena, redoviDobiti, skalaGrafikona, skalaSaMinusom, susjedniMjesec, zaokruziGore } from "./poslovanje.ts";

const TROSKOVI = {
  ambalaza: 12.5,
  ambalazaProcjena: false,
  gorivo: { km: 120.4, brojRuta: 6, poRutama: 30, poRacunima: 0, izvor: "rute" as const, uDobiti: 30, bezCijene: false },
  poVrsti: [
    { vrsta: "Zakup", jeGorivo: false, jednokratni: 0, ponavljajuci: 200, iznos: 200 },
    { vrsta: "Marketing", jeGorivo: false, jednokratni: 50, ponavljajuci: 0, iznos: 50 },
  ],
  ostali: 250,
  ukupno: 292.5,
  dobit: 207.5,
  marzaDobiti: 0.2075,
  varijabilniPoNarudzbi: 2.13,
};

test("redovi dobiti: neto, nabavno, ambalaža, gorivo, vrste, dobit", () => {
  const r = redoviDobiti({ neto: 1000, nabavno: 500 }, TROSKOVI);
  assert.deepEqual(
    r.map((x) => [x.stavka, x.iznos, x.vrsta]),
    [
      ["Neto prihod", 1000, "prihod"],
      ["Nabavna roba", -500, "trosak"],
      ["Ambalaža (uz prodaju)", -12.5, "trosak"],
      ["Gorivo — po rutama (120,4 km, 6 ruta)", -30, "trosak"],
      ["Zakup (mjesečno)", -200, "trosak"],
      ["Marketing", -50, "trosak"],
      ["Dobit", 207.5, "dobit"],
    ],
  );
});

test("redovi dobiti: gorivo po računima i ambalaža procjena", () => {
  const t = {
    ...TROSKOVI,
    ambalazaProcjena: true,
    gorivo: { ...TROSKOVI.gorivo, izvor: "racuni" as const, poRacunima: 80, uDobiti: 80 },
    poVrsti: [{ vrsta: "Gorivo", jeGorivo: true, jednokratni: 80, ponavljajuci: 0, iznos: 80 }],
  };
  const r = redoviDobiti({ neto: 1000, nabavno: 500 }, t);
  assert.equal(r[2].stavka, "Ambalaža (uz prodaju) *");
  assert.equal(r[3].stavka, "Gorivo — po računima");
  // vrsta goriva nije posebno, jer je već u redu goriva
  assert.equal(r.filter((x) => x.stavka.startsWith("Gorivo")).length, 1);
});

test("skala sa minusom: dno ose kad ima negativnih vrijednosti", () => {
  assert.deepEqual(skalaSaMinusom([100, 40, -30]), { max: 100, min: 50 });
  assert.deepEqual(skalaSaMinusom([100, 40]), { max: 100, min: 0 });
});

test("promjena u procentima, bez dijeljenja nulom", () => {
  assert.equal(promjena(120, 100), 20);
  assert.equal(promjena(80, 100), -20);
  assert.equal(promjena(5, 0), null);
  assert.equal(promjena(0, 0), null);
  assert.equal(promjena(1, 3), -66.7);
});

test("procenat iz udjela 0–1, prazno je crtica", () => {
  assert.equal(procenat(0.619), "61,9%");
  assert.equal(procenat(null), "—");
  assert.equal(procenat(undefined), "—");
});

test("susjedni mjesec preko granice godine", () => {
  assert.deepEqual(susjedniMjesec(2026, 1, -1), { godina: 2025, mjesec: 12 });
  assert.deepEqual(susjedniMjesec(2026, 12, 1), { godina: 2027, mjesec: 1 });
  assert.deepEqual(susjedniMjesec(2026, 10, 1), { godina: 2026, mjesec: 11 });
});

test("skala grafikona zaokružuje gore na 1/2/5 × 10^n i ima pet podjela", () => {
  assert.equal(zaokruziGore(0), 1);
  assert.equal(zaokruziGore(1234), 2000);
  assert.equal(zaokruziGore(4100), 5000);
  assert.equal(zaokruziGore(5000), 5000);
  assert.deepEqual(skalaGrafikona([0, 3200, 1500]), { max: 5000, podjele: [0, 1250, 2500, 3750, 5000] });
  assert.equal(skalaGrafikona([-50, 0]).max, 1); // gubitak se crta od nule, skala prati pozitivne
});
