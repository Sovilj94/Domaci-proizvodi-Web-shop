import { db } from "@/server/mock/store";
import { jsonOk, requireAdmin } from "@/server/mock/http";

export function GET(req: Request) {
  const { response } = requireAdmin(req);
  if (response) return response;
  return jsonOk(db.drivers, { total: db.drivers.length });
}
