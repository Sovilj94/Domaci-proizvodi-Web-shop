import type { NextRequest } from "next/server";
import { db, upsertProduct } from "@/server/mock/store";
import { jsonFail, jsonOk, requireAdmin } from "@/server/mock/http";
import { validateProduct } from "@/server/mock/validation";

export function GET(req: NextRequest) {
  const { response } = requireAdmin(req);
  if (response) return response;
  const products = [...db.products].sort((a, b) => a.name.localeCompare(b.name));
  return jsonOk(products, { total: products.length });
}

export async function POST(req: NextRequest) {
  const { response } = requireAdmin(req);
  if (response) return response;

  const body = await req.json().catch(() => null);
  const details = validateProduct(body);
  if (Object.keys(details).length > 0) {
    return jsonFail("validation_error", "Provjerite unesene podatke.", 400, details);
  }
  const product = upsertProduct(body);
  if (!product) return jsonFail("invalid_reference", "Nepoznata kategorija ili proizvođač.", 400);
  return jsonOk(product, undefined, 201);
}
