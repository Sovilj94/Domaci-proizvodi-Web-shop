import { db } from "@/server/mock/store";
import { jsonOk } from "@/server/mock/http";

export function GET() {
  return jsonOk(db.slots, { total: db.slots.length });
}
