import type { ApiError, ApiMeta, ApiResponse } from "./envelope";
import { procitajPismo } from "@/lib/pismo/pismo";

/**
 * Base URL for the ASP.NET Core API — the only source of data; there is no built-in mock.
 * Set NEXT_PUBLIC_API_URL (e.g. https://epiljara-api.onrender.com); NEXT_PUBLIC_API_BASE_URL is
 * the older name and still works. Unset falls back to the local `dotnet run` address, so a
 * missing variable fails visibly instead of showing fake data. Inlined at build time — a change
 * on Vercel needs a redeploy.
 */
export const API_BASE =
  (process.env.NEXT_PUBLIC_API_URL || process.env.NEXT_PUBLIC_API_BASE_URL)?.replace(/\/$/, "") ||
  "http://localhost:5080";

const TOKEN_KEY = "epiljara.token";

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null): void {
  if (typeof window === "undefined") return;
  if (token) window.localStorage.setItem(TOKEN_KEY, token);
  else window.localStorage.removeItem(TOKEN_KEY);
}

export class ApiRequestError extends Error {
  constructor(
    public readonly status: number,
    public readonly apiError: ApiError,
  ) {
    super(apiError.message);
    this.name = "ApiRequestError";
  }

  /** Field-level validation errors, if the server returned any. */
  get fieldErrors(): Record<string, string[]> {
    return this.apiError.details ?? {};
  }
}

export type QueryPrimitive = string | number | boolean | undefined | null;
type Query = Record<string, QueryPrimitive>;

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  /** Accepts typed query objects; values are coerced to strings. */
  query?: Record<string, QueryPrimitive>;
  /** Attach the bearer token if one is stored. Defaults to true. */
  auth?: boolean;
  signal?: AbortSignal;
}

function buildUrl(path: string, query?: Query): string {
  const url = `${API_BASE}${path}`;
  if (!query) return url;
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== "") {
      params.set(key, String(value));
    }
  }
  const qs = params.toString();
  return qs ? `${url}?${qs}` : url;
}

export interface ApiResult<T> {
  data: T;
  meta: ApiMeta | null;
}

export async function apiFetch<T>(
  path: string,
  { method = "GET", body, query, auth = true, signal }: RequestOptions = {},
): Promise<ApiResult<T>> {
  const headers: Record<string, string> = { Accept: "application/json" };
  // FormData (upload slike) ide kakva jeste — pregledač sam postavi multipart granicu.
  const forma = body instanceof FormData;
  if (body !== undefined && !forma) headers["Content-Type"] = "application/json";
  // Server po ovome bira pismo mejla i Vibera za narudžbu, zahtjev gosta i novi nalog.
  if (procitajPismo() === "cir") headers["X-Pismo"] = "cir";

  if (auth) {
    const token = getToken();
    if (token) headers["Authorization"] = `Bearer ${token}`;
  }

  let res: Response;
  try {
    res = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: forma ? body : body !== undefined ? JSON.stringify(body) : undefined,
      signal,
      cache: "no-store",
    });
  } catch {
    throw new ApiRequestError(0, {
      code: "network_error",
      message: "Nije moguće povezati se sa serverom. Provjeri konekciju.",
    });
  }

  /*
    204 No Content je uspjeh bez tijela, ne greška. Bez ove grane `res.json()` pukne na
    praznom tijelu, `payload` ostane null, pa uspješno brisanje izleti kao
    ApiRequestError(204) — što je upravo zbunilo poziv za brisanje adrese.
    Danas je `DELETE /account/addresses/{id}` jedini takav endpoint; svi ostali DELETE-ovi
    vraćaju omotnicu, pa ih ova grana ne dira.
  */
  if (res.status === 204) return { data: undefined as T, meta: null };

  let payload: ApiResponse<T> | null = null;
  try {
    payload = (await res.json()) as ApiResponse<T>;
  } catch {
    // Non-JSON response (e.g. an unexpected 500 HTML page).
  }

  if (!res.ok || !payload || payload.error) {
    throw new ApiRequestError(
      res.status,
      payload?.error ?? {
        code: "unexpected_error",
        message: `Neočekivana greška (${res.status}).`,
      },
    );
  }

  return { data: payload.data as T, meta: payload.meta ?? null };
}
