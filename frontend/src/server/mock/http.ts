import { NextResponse } from "next/server";
import { fail, ok, type ApiMeta } from "@/lib/api/envelope";
import type { AuthUser } from "@/types/domain";
import { userByToken } from "./store";

export function jsonOk<T>(data: T, meta?: ApiMeta, status = 200): NextResponse {
  return NextResponse.json(ok(data, meta), { status });
}

export function jsonFail(
  code: string,
  message: string,
  status: number,
  details?: Record<string, string[]>,
): NextResponse {
  return NextResponse.json(fail(code, message, details), { status });
}

export function bearer(req: Request): string | null {
  const header = req.headers.get("authorization");
  if (!header?.startsWith("Bearer ")) return null;
  return header.slice(7);
}

export function currentUser(req: Request): AuthUser | null {
  return userByToken(bearer(req));
}

export function requireAdmin(
  req: Request,
): { user: AuthUser; response?: undefined } | { user?: undefined; response: NextResponse } {
  const user = currentUser(req);
  if (!user) return { response: jsonFail("unauthorized", "Prijava je obavezna.", 401) };
  if (user.role !== "admin") {
    return { response: jsonFail("forbidden", "Nemate pristup ovom resursu.", 403) };
  }
  return { user };
}

export function requireUser(
  req: Request,
): { user: AuthUser; response?: undefined } | { user?: undefined; response: NextResponse } {
  const user = currentUser(req);
  if (!user) return { response: jsonFail("unauthorized", "Prijava je obavezna.", 401) };
  return { user };
}
