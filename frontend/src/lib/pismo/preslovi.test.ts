import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { uCirilicu, uLatinicu } from "./preslovi.ts";

type Vektor = { lat: string; cir: string; povratno?: boolean };

const vektori: Vektor[] = JSON.parse(
  readFileSync(new URL("../../../../docs/pismo-vektori.json", import.meta.url), "utf8"),
);

for (const v of vektori) {
  test(`uCirilicu: ${v.lat}`, () => {
    assert.equal(uCirilicu(v.lat), v.cir);
  });

  test(`idempotentno: ${v.cir}`, () => {
    assert.equal(uCirilicu(v.cir), v.cir);
  });

  if (v.povratno) {
    test(`uLatinicu: ${v.cir}`, () => {
      assert.equal(uLatinicu(v.cir), v.lat);
    });
  }
}

test("uLatinicu za pretragu", () => {
  assert.equal(uLatinicu("паприка"), "paprika");
  assert.equal(uLatinicu("Шљива"), "Šljiva");
});
