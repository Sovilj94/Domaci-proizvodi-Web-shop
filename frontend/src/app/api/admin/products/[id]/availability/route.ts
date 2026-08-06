import type { NextRequest } from "next/server";
import { setProductAvailability } from "@/server/mock/store";
import { jsonFail, jsonOk, requireAdmin } from "@/server/mock/http";

export async function PATCH(req: NextRequest, ctx: { params: Promise<{ id: string }> }) {
  const { response } = requireAdmin(req);
  if (response) return response;

  const { id } = await ctx.params;
  const body = await req.json().catch(() => null);
  if (typeof body?.isAvailable !== "boolean") {
    return jsonFail("validation_error", "isAvailable mora biti true/false.", 400);
  }
  const product = setProductAvailability(id, body.isAvailable);
  if (!product) return jsonFail("not_found", "Proizvod nije pronađen.", 404);
  return jsonOk(product);
}
