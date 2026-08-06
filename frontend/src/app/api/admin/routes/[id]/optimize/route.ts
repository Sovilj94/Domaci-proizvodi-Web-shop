import type { NextRequest } from "next/server";
import { optimizeRoute } from "@/server/mock/store";
import { jsonFail, jsonOk, requireAdmin } from "@/server/mock/http";

export async function POST(req: NextRequest, ctx: { params: Promise<{ id: string }> }) {
  const { response } = requireAdmin(req);
  if (response) return response;
  const { id } = await ctx.params;
  const route = optimizeRoute(id);
  if (!route) return jsonFail("not_found", "Ruta nije pronađena.", 404);
  return jsonOk(route);
}
