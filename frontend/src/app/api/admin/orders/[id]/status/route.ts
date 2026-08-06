import type { NextRequest } from "next/server";
import { updateOrderStatus } from "@/server/mock/store";
import { jsonFail, jsonOk, requireAdmin } from "@/server/mock/http";
import { ORDER_STATUSES, type OrderStatus } from "@/types/domain";

export async function PATCH(req: NextRequest, ctx: { params: Promise<{ id: string }> }) {
  const { response } = requireAdmin(req);
  if (response) return response;

  const { id } = await ctx.params;
  const body = await req.json().catch(() => null);
  const status = body?.status as OrderStatus | undefined;
  if (!status || !ORDER_STATUSES.includes(status)) {
    return jsonFail("validation_error", "Nepoznat status narudžbe.", 400);
  }
  const order = updateOrderStatus(id, status);
  if (!order) return jsonFail("not_found", "Narudžba nije pronađena.", 404);
  return jsonOk(order);
}
