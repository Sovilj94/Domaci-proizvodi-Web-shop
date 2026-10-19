import { test } from "node:test";
import assert from "node:assert/strict";
import { KONTAKT, kontaktIz } from "./kontakt.ts";

test("bez sadržaja iz admina važe ugrađeni kontakt podaci", () => {
  const k = kontaktIz(undefined);
  assert.equal(k.telefon, KONTAKT.telefon);
  assert.equal(k.telefonHref, KONTAKT.telefonHref);
  assert.equal(k.email, null);
});

test("telefon iz admina daje link za poziv samo sa ciframa i plusom", () => {
  const k = kontaktIz({ telefon: "+387 (65) 999-888" });
  assert.equal(k.telefon, "+387 (65) 999-888");
  assert.equal(k.telefonHref, "tel:+38765999888");
  assert.equal(k.adresa, KONTAKT.adresa);
});
