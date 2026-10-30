import { test } from "node:test";
import assert from "node:assert/strict";
import type { Order, OrderItem, OrderStatus } from "@/types/domain";
import { saberi } from "./zbir.ts";

const st = (productId: string, productName: string, quantity: number, extra: Partial<OrderItem> = {}) =>
  ({ id: `${productId}-${quantity}`, productId, productName, quantity, unit: "kom", unitPrice: 1, ...extra }) as OrderItem;
const n = (status: OrderStatus, items: OrderItem[]) => ({ id: Math.random().toString(), status, items }) as Order;

test("korpice i ostalo se sabiraju odvojeno, po statusu", () => {
  const z = saberi([
    n("Pending", [st("mala", "Mala korpica povrća", 1, { jeKorpica: true }), st("med", "Livadski med", 1.5, { unit: "kg" })]),
    n("Pending", [st("mala", "Mala korpica povrća", 2, { jeKorpica: true })]),
    n("Confirmed", [st("mala", "Mala korpica povrća", 1, { jeKorpica: true }), st("med", "Livadski med", 0.5, { unit: "kg" })]),
  ]);
  assert.deepEqual(z.korpice, [
    { productId: "mala", naziv: "Mala korpica povrća", jedinica: "kom", naCekanju: 3, potvrdjeno: 1, ukupno: 4 },
  ]);
  assert.deepEqual(z.ostalo, [
    { productId: "med", naziv: "Livadski med", jedinica: "kg", naCekanju: 1.5, potvrdjeno: 0.5, ukupno: 2 },
  ]);
});

test("narudžbe poslije potvrde i otkazane ne ulaze u zbir", () => {
  const z = saberi(
    (["Packing", "OutForDelivery", "Delivered", "FailedDelivery", "Cancelled"] as const).map((s) =>
      n(s, [st("jaja", "Jaja", 10)]),
    ),
  );
  assert.deepEqual(z, { korpice: [], ostalo: [] });
});

test("redovi su poredani po nazivu, a količine zaokružene na dvije decimale", () => {
  const z = saberi([
    n("Pending", [st("z", "Žuti sir", 0.1, { unit: "kg" }), st("s", "Šljiva", 1), st("v", "Svježe mlijeko", 1)]),
    n("Pending", [st("z", "Žuti sir", 0.2, { unit: "kg" })]),
  ]);
  assert.deepEqual(z.ostalo.map((r) => r.naziv), ["Svježe mlijeko", "Šljiva", "Žuti sir"], "latinični red: S prije Š");
  assert.equal(z.ostalo[2].ukupno, 0.3);
});
