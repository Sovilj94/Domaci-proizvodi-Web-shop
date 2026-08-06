import { jsonOk } from "@/server/mock/http";

/** Mock resend — always succeeds (the real API re-sends the confirmation email). */
export async function POST() {
  return jsonOk({ message: "Ako nalog postoji i nije potvrđen, poslali smo novi link." });
}
