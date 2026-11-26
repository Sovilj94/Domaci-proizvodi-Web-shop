import { test } from "node:test";
import assert from "node:assert/strict";
import { godinaZadnjeSezone, popuniSezonu } from "./mjeseci.ts";

test("popuniSezonu zamjenjuje sve oznake u pravom padežu", () => {
  assert.equal(
    popuniSezonu("Voće se vraća u {u}. Slažemo od {od} do {do}; sezona {godina}.", { od: 5, do: 10, godina: 2026 }),
    "Voće se vraća u maju. Slažemo od maja do oktobra; sezona 2026.",
  );
});

test("godinaZadnjeSezone: poslije kraja sezone ista godina, prije kraja prošla", () => {
  assert.equal(godinaZadnjeSezone(10, new Date(2026, 10, 26)), 2026);
  assert.equal(godinaZadnjeSezone(10, new Date(2027, 0, 15)), 2026);
});
