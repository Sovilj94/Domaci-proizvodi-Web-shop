import { test } from "node:test";
import assert from "node:assert/strict";
import { formatMarza, marza, tonMarze } from "./marza.ts";

test("marža je udio prodajne cijene, bez nabavne je nema", () => {
  assert.equal(marza(5, 3), 0.4);
  assert.equal(marza(5, null), null);
  assert.equal(marza(5, 0), null);
  assert.equal(marza(0, 3), null);
});

test("marža se ispisuje u procentima, i negativna", () => {
  assert.equal(formatMarza(0.4), "40%");
  assert.equal(formatMarza(-0.04), "−4%");
  assert.equal(formatMarza(null), "—");
});

test("ton marže prema minimumu", () => {
  assert.equal(tonMarze(0.2), "losa");
  assert.equal(tonMarze(0.3), "tijesna");
  assert.equal(tonMarze(0.5), "dobra");
  assert.equal(tonMarze(null), "nema");
});
