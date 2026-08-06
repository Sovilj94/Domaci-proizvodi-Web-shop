import type { NextRequest } from "next/server";
import { dashboard } from "@/server/mock/store";
import { jsonOk, requireAdmin } from "@/server/mock/http";

export function GET(req: NextRequest) {
  const { response } = requireAdmin(req);
  if (response) return response;
  const date = new URL(req.url).searchParams.get("date") ?? undefined;
  return jsonOk(dashboard(date));
}
