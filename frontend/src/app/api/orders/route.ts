import type { NextRequest } from "next/server";
import { createOrder } from "@/server/mock/store";
import { currentUser, jsonFail, jsonOk } from "@/server/mock/http";

export async function POST(req: NextRequest) {
  const body = await req.json().catch(() => null);
  if (!body) return jsonFail("validation_error", "Neispravan zahtjev.", 400);

  const details: Record<string, string[]> = {};
  if (!body.customer?.name) details["customer.name"] = ["Ime je obavezno."];
  if (!body.customer?.phone) details["customer.phone"] = ["Telefon je obavezan."];
  if (!body.deliveryZoneId) details.deliveryZoneId = ["Odaberite zonu dostave."];
  if (!body.deliverySlotId) details.deliverySlotId = ["Odaberite termin dostave."];
  if (!body.deliveryAddress) details.deliveryAddress = ["Adresa je obavezna."];
  if (!body.items?.length) details.items = ["Korpa je prazna."];
  if (Object.keys(details).length > 0) {
    return jsonFail("validation_error", "Provjerite unesene podatke.", 400, details);
  }

  const user = currentUser(req);
  const { order, error } = createOrder(body, user?.id ?? null);
  if (error) return jsonFail("order_rejected", error, 400);
  return jsonOk(order, undefined, 201);
}
