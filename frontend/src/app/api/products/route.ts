import type { NextRequest } from "next/server";
import { db } from "@/server/mock/store";
import { jsonOk } from "@/server/mock/http";

export function GET(req: NextRequest) {
  const { searchParams } = new URL(req.url);
  const category = searchParams.get("category");
  const search = searchParams.get("search")?.toLowerCase();
  const featured = searchParams.get("featured");

  let items = [...db.products];
  if (category) items = items.filter((p) => p.categorySlug === category);
  if (featured === "true") items = items.filter((p) => p.isFeatured);
  if (search) {
    items = items.filter(
      (p) =>
        p.name.toLowerCase().includes(search) ||
        p.description.toLowerCase().includes(search) ||
        p.categoryName.toLowerCase().includes(search),
    );
  }
  // Available products first, then alphabetical.
  items.sort(
    (a, b) => Number(b.isAvailable) - Number(a.isAvailable) || a.name.localeCompare(b.name),
  );

  return jsonOk(items, { total: items.length });
}
