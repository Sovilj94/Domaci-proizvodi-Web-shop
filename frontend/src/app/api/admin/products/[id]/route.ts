import type { NextRequest } from "next/server";
import { upsertProduct } from "@/server/mock/store";
import { jsonFail, jsonOk, requireAdmin } from "@/server/mock/http";
import { validateProduct } from "@/server/mock/validation";

export async function PUT(req: NextRequest, ctx: { params: Promise<{ id: string }> }) {
  const { response } = requireAdmin(req);
  if (response) return response;

  const { id } = await ctx.params;
  const body = await req.json().catch(() => null);
  const details = validateProduct(body);
  if (Object.keys(details).length > 0) {
    return jsonFail("validation_error", "Provjerite unesene podatke.", 400, details);
  }
  const product = upsertProduct(body, id);
  if (!product) return jsonFail("not_found", "Proizvod nije pronađen.", 404);
  return jsonOk(product);
}
