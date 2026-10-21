import { test } from "node:test";
import assert from "node:assert/strict";
import { ocjena, rangiraj } from "./paleta.ts";

test("ocjena: početak > početak riječi > sadrži, bez kvačica", () => {
  assert.equal(ocjena("Čvarci", "cvar"), 3);
  assert.equal(ocjena("Mladi kravlji sir", "krav"), 2);
  assert.equal(ocjena("Babura paprika", "prik"), 1);
  assert.equal(ocjena("Malina", "kruška"), 0);
});

test("više riječi: sve moraju odgovarati", () => {
  assert.equal(ocjena("Jabuka zlatni delišes", "jab del"), 2);
  assert.equal(ocjena("Jabuka idared", "jab del"), 0);
});

test("rangiranje stavlja bolje pogotke gore i čuva redoslijed jednakih", () => {
  const r = rangiraj(["Paprika ljuta", "Babura paprika", "Ljuta paprika", "Malina"], "pap", (s) => s);
  assert.deepEqual(r, ["Paprika ljuta", "Babura paprika", "Ljuta paprika"]);
  assert.deepEqual(rangiraj(["a", "b", "c"], "", (s) => s, 2), ["a", "b"]);
});
