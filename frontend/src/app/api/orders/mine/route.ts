import { ordersForUser } from "@/server/mock/store";
import { jsonOk, requireUser } from "@/server/mock/http";

export function GET(req: Request) {
  const { user, response } = requireUser(req);
  if (response) return response;
  const orders = ordersForUser(user.id);
  return jsonOk(orders, { total: orders.length });
}
