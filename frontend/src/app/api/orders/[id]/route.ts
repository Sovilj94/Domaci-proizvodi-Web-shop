import { db } from "@/server/mock/store";
import { jsonFail, jsonOk, requireUser } from "@/server/mock/http";

export async function GET(req: Request, ctx: { params: Promise<{ id: string }> }) {
  const { user, response } = requireUser(req);
  if (response) return response;
  const { id } = await ctx.params;
  const order = db.orders.find((o) => o.id === id);
  if (!order) return jsonFail("not_found", "Narudžba nije pronađena.", 404);
  if (order.customerId !== user.id && user.role !== "admin") {
    return jsonFail("forbidden", "Nemate pristup ovoj narudžbi.", 403);
  }
  return jsonOk(order);
}
