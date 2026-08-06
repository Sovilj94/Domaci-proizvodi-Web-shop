import { db } from "@/server/mock/store";
import { jsonFail, jsonOk } from "@/server/mock/http";

export async function GET(
  _req: Request,
  ctx: { params: Promise<{ slug: string }> },
) {
  const { slug } = await ctx.params;
  const product = db.products.find((p) => p.slug === slug);
  if (!product) return jsonFail("not_found", "Proizvod nije pronađen.", 404);
  return jsonOk(product);
}
