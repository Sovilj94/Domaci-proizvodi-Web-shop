import type { NextRequest } from "next/server";
import { assignRoute, routesForDate } from "@/server/mock/store";
import { jsonFail, jsonOk, requireAdmin } from "@/server/mock/http";

export function GET(req: NextRequest) {
  const { response } = requireAdmin(req);
  if (response) return response;
  const date = new URL(req.url).searchParams.get("date");
  if (!date) return jsonFail("validation_error", "Parametar 'date' je obavezan.", 400);
  const routes = routesForDate(date);
  return jsonOk(routes, { total: routes.length });
}

export async function POST(req: NextRequest) {
  const { response } = requireAdmin(req);
  if (response) return response;

  const body = await req.json().catch(() => null);
  if (!body?.date || !body?.driverId || !Array.isArray(body?.orderIds)) {
    return jsonFail("validation_error", "date, driverId i orderIds su obavezni.", 400);
  }
  const route = assignRoute(body);
  if (!route) return jsonFail("not_found", "Vozač nije pronađen.", 404);
  return jsonOk(route, undefined, 201);
}
