import { test } from "node:test";
import assert from "node:assert/strict";
import type { Address } from "@/types/domain";
import { podrazumijevanaAdresa, ranijeBezAdresara, sacuvajPrimaoca } from "./checkout-adrese.ts";

const a = (id: string, fullAddress: string, extra: Partial<Address> = {}): Address => ({
  id,
  fullAddress,
  deliveryZoneActive: true,
  isDefault: false,
  ...extra,
});
const mama = a("m", "Kozaračka 5", { isDefault: true, primalacIme: "Petar", primalacTelefon: "+387 65 1" });
const moja = a("k", "Bulevar 1");

test("podrazumijevana adresa nikad nije adresa sa primaocem", () => {
  assert.equal(podrazumijevanaAdresa([mama, moja], ["Stara 3"]), moja);
});

test("podrazumijevana uzima glavnu kad je moja", () => {
  const glavna = a("g", "Glavna 2", { isDefault: true });
  assert.equal(podrazumijevanaAdresa([moja, glavna], []), glavna);
});

test("kad su sve adrese za druge, uzima raniju adresu", () => {
  assert.equal(podrazumijevanaAdresa([mama], ["Stara 3"]), "Stara 3");
});

test("bez ičega nema podrazumijevane", () => {
  assert.equal(podrazumijevanaAdresa([], []), null);
});

test("ranije adrese koje su već u adresaru se ne ponavljaju", () => {
  assert.deepEqual(ranijeBezAdresara([mama], [" kozaračka 5 ", "Stara 3"]), ["Stara 3"]);
});

const nePozivaj = async () => {
  throw new Error("API ne smije biti pozvan");
};
const unos = { fullAddress: "Kozaračka 5", primalacIme: "Petar", primalacTelefon: "+387 65 1" };

test("postojeća adresa se ne čuva ponovo", async () => {
  assert.equal(await sacuvajPrimaoca([mama], unos, nePozivaj), "postoji");
});

test("pun adresar se ne dira", async () => {
  const deset = Array.from({ length: 10 }, (_, i) => a(String(i), `Ulica ${i}`));
  assert.equal(await sacuvajPrimaoca(deset, unos, nePozivaj), "pun");
});

test("greška pri čuvanju ne baca", async () => {
  assert.equal(await sacuvajPrimaoca([], unos, nePozivaj), "greska");
});

test("nova adresa se čuva", async () => {
  let poslano: unknown;
  const r = await sacuvajPrimaoca([], unos, async (b) => {
    poslano = b;
    return a("n", b.fullAddress);
  });
  assert.equal(r, "sacuvano");
  assert.deepEqual(poslano, unos);
});
