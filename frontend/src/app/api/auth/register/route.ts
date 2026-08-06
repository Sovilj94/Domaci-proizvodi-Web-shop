import type { NextRequest } from "next/server";
import { registerUser } from "@/server/mock/store";
import { jsonFail, jsonOk } from "@/server/mock/http";

export async function POST(req: NextRequest) {
  const body = await req.json().catch(() => null);
  const details: Record<string, string[]> = {};
  if (!body?.name) details.name = ["Ime je obavezno."];
  if (!body?.email) details.email = ["Email je obavezan."];
  if (!body?.phone) details.phone = ["Telefon je obavezan."];
  if (!body?.password || body.password.length < 6) {
    details.password = ["Lozinka mora imati najmanje 6 znakova."];
  }
  if (Object.keys(details).length > 0) {
    return jsonFail("validation_error", "Provjerite unesene podatke.", 400, details);
  }

  const user = registerUser({
    name: body.name,
    email: body.email,
    phone: body.phone,
    password: body.password,
    address: body.address,
    deliveryZoneId: body.deliveryZoneId,
  });
  if (!user) {
    return jsonFail("email_taken", "Nalog sa ovim emailom već postoji.", 409);
  }
  // Registration no longer logs in — the real API emails a confirmation link.
  return jsonOk({ email: user.email, confirmationRequired: true }, undefined, 201);
}
