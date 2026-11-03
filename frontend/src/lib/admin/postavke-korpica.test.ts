import { test } from "node:test";
import assert from "node:assert/strict";
import { ciljPoKulturi, ciljSadrzaja, nabavniBudzet, ocjenaKolicine, predlozenaKolicina } from "./postavke-korpica.ts";

test("cilj sadržaja, po kulturi i nabavni budžet", () => {
  assert.equal(ciljSadrzaja(35, 1.15), 40.25);
  assert.equal(ciljPoKulturi(35, 1.15, 8), 5.03);
  assert.equal(ciljPoKulturi(35, 1.15, 0), 40.25, "nula kultura se tretira kao jedna");
  assert.equal(nabavniBudzet(35, 0.25), 26.25);
});

test("ocjena: ispod pola kulture je malo, preko 1,6× je puno", () => {
  assert.equal(ocjenaKolicine(1, 2, 5), "malo");
  assert.equal(ocjenaKolicine(2.5, 2, 5), "ok");
  assert.equal(ocjenaKolicine(5, 2, 5), "puno");
  assert.equal(ocjenaKolicine(5, 2, 0), "ok", "bez cilja nema ocjene");
});

test("prijedlog: komadi cijeli, kg iznad 1 na pola, ispod 1 na desetinu", () => {
  assert.equal(predlozenaKolicina(5, 1.5, "kom"), 3);
  assert.equal(predlozenaKolicina(5, 20, "kom"), 1);
  assert.equal(predlozenaKolicina(5, 1.5, "kg"), 3.5);
  assert.equal(predlozenaKolicina(5, 12, "kg"), 0.4);
  assert.equal(predlozenaKolicina(5, 100, "kg"), 0.1);
});
