import { test } from "node:test";
import assert from "node:assert/strict";
import { razlogNeuspjeha } from "./orders.ts";

test("razlog iz vozačke aplikacije se prikazuje čitljivo", () => {
  assert.equal(razlogNeuspjeha("NijeKodKuce"), "Kupac nije kod kuće");
  assert.equal(razlogNeuspjeha("OdbioPrijem"), "Odbio prijem");
});

test("bez razloga (stara aplikacija) nema teksta, a nepoznat razlog ostaje kakav jeste", () => {
  assert.equal(razlogNeuspjeha(null), null);
  assert.equal(razlogNeuspjeha(undefined), null);
  assert.equal(razlogNeuspjeha("NoviRazlog"), "NoviRazlog");
});
