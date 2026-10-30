import { test } from "node:test";
import assert from "node:assert/strict";
import { kontaktIz } from "./kontakt.ts";

test("bez sadržaja iz baze nema ni kontakta — sajt nema ugrađene podatke", () => {
  assert.deepEqual(kontaktIz(undefined), { telefon: null, telefonHref: null, email: null, adresa: null, podrucje: null });
});

test("telefon iz baze daje link za poziv samo sa ciframa i plusom", () => {
  const k = kontaktIz({ telefon: "+387 (65) 999-888", adresa: "Manjača" });
  assert.equal(k.telefon, "+387 (65) 999-888");
  assert.equal(k.telefonHref, "tel:+38765999888");
  assert.equal(k.adresa, "Manjača");
  assert.equal(k.podrucje, null);
});
