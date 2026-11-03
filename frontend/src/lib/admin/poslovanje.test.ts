import { test } from "node:test";
import assert from "node:assert/strict";
import { procenat, promjena, skalaGrafikona, susjedniMjesec, zaokruziGore } from "./poslovanje.ts";

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
