import { test } from "node:test";
import assert from "node:assert/strict";
import type { Order, OrderStatus } from "@/types/domain";
import { KOLONE, mozePreci, rasporedi, sljedeci } from "./kanban.ts";

const n = (code: string, status: OrderStatus, createdAt: string) => ({ id: code, code, status, createdAt }) as Order;

test("prevlačenje prati prelaze sa backenda", () => {
  assert.equal(mozePreci("Confirmed", "Packing"), true);
  assert.equal(mozePreci("Confirmed", "OutForDelivery"), false, "ne preskače korak");
  assert.equal(mozePreci("Packing", "Cancelled"), false, "pakovanje se ne otkazuje");
  assert.equal(mozePreci("Delivered", "Confirmed"), false, "nema nazad");
  assert.equal(mozePreci("Confirmed", "Confirmed"), false);
});

test("sljedeći korak preskače otkazivanje i neuspjeh", () => {
  assert.equal(sljedeci("Confirmed"), "Packing");
  assert.equal(sljedeci("OutForDelivery"), "Delivered");
  assert.equal(sljedeci("FailedDelivery"), "OutForDelivery");
  assert.equal(sljedeci("Delivered"), null);
});

test("nema kolone za potvrđivanje — narudžba je prihvaćena čim nastane", () => {
  assert.equal(KOLONE.includes("Pending"), false);
  assert.equal(KOLONE[0], "Confirmed");
});

test("stara narudžba na čekanju ide među prihvaćene", () => {
  const t = rasporedi([n("A", "Pending", "2026-10-21T10:00"), n("B", "Confirmed", "2026-10-20T10:00")]);
  assert.deepEqual(t.kolone.Confirmed.map((o) => o.code), ["B", "A"]);
});

test("raspored: red čekanja najstariji prvi, dostavljene najnovije prve, problemi i otkazane odvojeno", () => {
  const t = rasporedi([
    n("A", "Confirmed", "2026-10-21T10:00"),
    n("B", "Confirmed", "2026-10-20T10:00"),
    n("C", "Delivered", "2026-10-19T10:00"),
    n("D", "Delivered", "2026-10-21T09:00"),
    n("E", "FailedDelivery", "2026-10-21T08:00"),
    n("F", "Cancelled", "2026-10-21T08:00"),
  ]);
  assert.deepEqual(t.kolone.Confirmed.map((o) => o.code), ["B", "A"]);
  assert.deepEqual(t.kolone.Delivered.map((o) => o.code), ["D", "C"]);
  assert.deepEqual(t.problemi.map((o) => o.code), ["E"]);
  assert.equal(t.otkazane, 1);
});
