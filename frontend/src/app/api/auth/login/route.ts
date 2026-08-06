import type { NextRequest } from "next/server";
import { authenticate } from "@/server/mock/store";
import { jsonFail, jsonOk } from "@/server/mock/http";

export async function POST(req: NextRequest) {
  const body = await req.json().catch(() => null);
  if (!body?.email || !body?.password) {
    return jsonFail("validation_error", "Email i lozinka su obavezni.", 400);
  }
  const user = authenticate(body.email, body.password);
  if (!user) {
    return jsonFail("invalid_credentials", "Pogrešan email ili lozinka.", 401);
  }
  // Mock token = user id. The real API issues a signed JWT here.
  return jsonOk({ token: user.id, user });
}
