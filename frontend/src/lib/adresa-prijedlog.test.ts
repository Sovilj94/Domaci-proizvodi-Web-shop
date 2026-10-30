import { test } from "node:test";
import assert from "node:assert/strict";
import { redoviPrijedloga, tekstIzPogotka } from "./adresa-prijedlog.ts";

const kuca = {
  displayName: "Gundulićeva 10, Banja Luka",
  street: "Gundulićeva",
  houseNumber: "10",
  city: "Banja Luka",
};

test("checkout: ulica ide u polje, broj u svoje polje", () => {
  assert.deepEqual(tekstIzPogotka(kuca, true), { tekst: "Gundulićeva", broj: "10" });
});

test("checkout: ulica bez broja ne briše upisani broj", () => {
  assert.deepEqual(tekstIzPogotka({ ...kuca, houseNumber: null }, true), {
    tekst: "Gundulićeva",
    broj: null,
  });
});

test("bez zasebnog broja polje dobija kratku cijelu adresu", () => {
  assert.deepEqual(tekstIzPogotka(kuca, false), {
    tekst: "Gundulićeva 10, Banja Luka",
    broj: null,
  });
});

test("bez ulice ostaje puni opis geokodera", () => {
  const park = { displayName: "Park Mladen Stojanović, Banja Luka", street: null };
  assert.equal(tekstIzPogotka(park, true).tekst, "Park Mladen Stojanović, Banja Luka");
});

test("redovi prijedloga: adresa podebljano, grad ispod; objekat zadržava ime", () => {
  assert.deepEqual(redoviPrijedloga(kuca), { naslov: "Gundulićeva 10", podnaslov: "Banja Luka" });
  assert.deepEqual(
    redoviPrijedloga({
      displayName: "Pekara Mrvica, Srpska 5, Banja Luka",
      street: "Srpska",
      houseNumber: "5",
      city: "Banja Luka",
    }),
    { naslov: "Pekara Mrvica · Srpska 5", podnaslov: "Banja Luka" },
  );
});
