// CLI-11: every call to the server goes through here. When the session token has ended (401),
// the caller asks for a new one with the refresh cookie and tries the call once more.

/** A failed call: the HTTP status and the server's stable error code ("" when there is none). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  /** Seconds to wait, from the Retry-After header (0 when there is none). */
  readonly retryAfter: number;

  constructor(status: number, code: string, retryAfter = 0) {
    super(`${status} ${code}`);
    this.status = status;
    this.code = code;
    this.retryAfter = retryAfter;
  }
}

/** The part of `fetch` that is used, so a test can stand in for the server. */
export type Send = (path: string, init: { method: string; headers: Record<string, string>; body: string | Blob | null }) =>
  Promise<{ status: number; ok: boolean; headers: { get(name: string): string | null }; json(): Promise<unknown> }>;

export type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

const RefreshPath = "/api/auth/refresh";
/** A 401 from these says "wrong code or password" or "no session at all", not "the token ended". */
const NeverRefresh = /^\/api\/(auth\/|dev\/login|logout)/;

/** A file goes as the raw body (the browser sets its Content-Type), with extra headers. Anything else goes as JSON. */
export function createCaller(send: Send): <T>(method: Method, path: string, body?: unknown, headers?: Record<string, string>) => Promise<T> {
  let refreshing: Promise<void> | null = null;

  const once = (method: Method, path: string, body?: unknown, headers: Record<string, string> = {}) => send(path, {
    method,
    headers: body === undefined || body instanceof Blob ? headers : { ...headers, "Content-Type": "application/json" },
    body: body === undefined ? null : body instanceof Blob ? body : JSON.stringify(body),
  });

  /** Calls that fail at the same moment share one refresh. A failed refresh shows in the second try of the call. */
  const refresh = () => refreshing ??= once("POST", RefreshPath)
    .then(() => undefined, () => undefined)
    .finally(() => { refreshing = null; });

  return async function call<T>(method: Method, path: string, body?: unknown, headers?: Record<string, string>): Promise<T> {
    let response = await once(method, path, body, headers);
    if (response.status === 401 && !NeverRefresh.test(path)) {
      await refresh();
      response = await once(method, path, body, headers);
    }

    const json: unknown = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) {
      throw new ApiError(
        response.status, (json as { error?: string } | null)?.error ?? "", Number(response.headers.get("Retry-After")) || 0);
    }
    return json as T;
  };
}
