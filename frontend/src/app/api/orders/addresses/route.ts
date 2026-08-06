import { jsonOk } from "@/server/mock/http";

/**
 * Mock saved-addresses. The mock has no per-user delivered-order history, so it
 * returns an empty list. The real API (orders/addresses) returns the customer's
 * distinct delivery addresses from their delivered orders, most recent first.
 */
export async function GET() {
  return jsonOk([]);
}
