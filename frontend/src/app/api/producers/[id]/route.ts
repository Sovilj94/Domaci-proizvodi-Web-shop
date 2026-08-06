import { db } from "@/server/mock/store";
import { jsonFail, jsonOk } from "@/server/mock/http";

export async function GET(_req: Request, ctx: { params: Promise<{ id: string }> }) {
  const { id } = await ctx.params;
  const producer = db.producers.find((p) => p.id === id);
  if (!producer) return jsonFail("not_found", "Proizvođač nije pronađen.", 404);
  return jsonOk(producer);
}
