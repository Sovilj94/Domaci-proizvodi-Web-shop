import { db } from "@/server/mock/store";
import { jsonOk } from "@/server/mock/http";

export function GET() {
  const categories = [...db.categories]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((c) => ({
      ...c,
      productCount: db.products.filter((p) => p.categoryId === c.id).length,
    }));
  return jsonOk(categories, { total: categories.length });
}
