import { db } from "@/server/mock/store";
import { jsonOk } from "@/server/mock/http";

export function GET() {
  const zones = db.zones.filter((z) => z.isActive);
  return jsonOk(zones, { total: zones.length });
}
