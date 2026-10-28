import { test } from "node:test";
import assert from "node:assert/strict";
import { absoluteUrl, SITE_URL, skratiOpis } from "./site.ts";

test("kratak opis ostaje isti, razmaci se sabijaju", () => {
  assert.equal(skratiOpis("  Svjež   sir  sa Manjače "), "Svjež sir sa Manjače");
  assert.equal(skratiOpis(""), undefined);
  assert.equal(skratiOpis(null), undefined);
});

test("dug opis se reže na granici riječi i dobija trotačku", () => {
  const dug = "Domaći ".repeat(40);
  const rez = skratiOpis(dug, 50)!;
  assert.ok(rez.length <= 50, `predugo: ${rez.length}`);
  assert.ok(rez.endsWith("Domaći…"), rez);
});

test("relativna putanja dobija domen, apsolutna ostaje", () => {
  assert.equal(absoluteUrl("/products/aronija"), `${SITE_URL}/products/aronija`);
  assert.equal(absoluteUrl("hero/x.jpg"), `${SITE_URL}/hero/x.jpg`);
  assert.equal(absoluteUrl("https://picsum.photos/1"), "https://picsum.photos/1");
});
