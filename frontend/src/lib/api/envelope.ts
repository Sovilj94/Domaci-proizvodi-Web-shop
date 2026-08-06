/**
 * The consistent response envelope every API endpoint returns:
 *   { data, error, meta }
 * Success -> data is set, error is null.
 * Failure -> data is null, error is set.
 */

export interface ApiError {
  code: string;
  message: string;
  /** Field-level validation errors, keyed by field name. */
  details?: Record<string, string[]>;
}

export interface ApiMeta {
  page?: number;
  pageSize?: number;
  total?: number;
  [key: string]: unknown;
}

export interface ApiResponse<T> {
  data: T | null;
  error: ApiError | null;
  meta?: ApiMeta | null;
}

export function ok<T>(data: T, meta?: ApiMeta): ApiResponse<T> {
  return { data, error: null, meta: meta ?? null };
}

export function fail(
  code: string,
  message: string,
  details?: Record<string, string[]>,
): ApiResponse<never> {
  return { data: null, error: { code, message, details } };
}
