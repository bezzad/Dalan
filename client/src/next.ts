// CLI-63: the address a person came from, kept across the login (also across the GitHub round trip). Pure, so tests run without a browser.

const NextKey = "dalan.next";
/** The consent screen of Login with Dalan (AUTH-41) keeps its whole query, PKCE challenge and state too. */
const MaxLength = 2048;

/** CLI-63: the path when it stays on this site (one `/` at the start, nothing that can mean another host), else null. */
export function safeNext(raw: string | null | undefined): string | null {
  if (typeof raw !== "string" || raw.length === 0 || raw.length > MaxLength) return null;
  if (!raw.startsWith("/") || raw.startsWith("//") || raw.startsWith("/\\")) return null;
  // eslint-disable-next-line no-control-regex
  if (raw.includes("\\") || /[\u0000-\u001f\u007f]/.test(raw)) return null;
  return raw;
}

/** CLI-63: keeps the path before the person leaves for GitHub. Storage that fails keeps nothing. */
export function rememberNext(path: string, storage?: Storage): void {
  const safe = safeNext(path);
  if (!safe) return;
  try {
    (storage ?? sessionStorage).setItem(NextKey, safe);
  } catch {
    // Nothing to keep: the login then stays on the current address.
  }
}

/** CLI-63: the kept path, once; it is cleared at the same time. */
export function takeNext(storage?: Storage): string | null {
  try {
    const store = storage ?? sessionStorage;
    const kept = safeNext(store.getItem(NextKey));
    store.removeItem(NextKey);
    return kept;
  } catch {
    return null;
  }
}

/** CLI-68: a link to another address starts at the top, unless it names a #part. */
export function startsAtTop(path: string): boolean {
  return !path.includes("#");
}

/** CLI-63: the address someone asked for: `?next=` first, then the kept path; null when neither is accepted. */
export function askedNext(search: string, kept: string | null): string | null {
  return safeNext(new URLSearchParams(search).get("next")) ?? safeNext(kept);
}

/** AUTH-41: the consent screen; its query is the site's request, so a login there keeps it. */
export const authorizePath = "/oauth/authorize";

/** CLI-63: where a finished login goes: the asked address, else the current one, and from `/` "My bots". */
export function loginTarget(pathname: string, search: string, kept: string | null): string {
  if (pathname === authorizePath) return pathname + search;
  return askedNext(search, kept) ?? (pathname === "/" || pathname === "" ? "/bots" : pathname);
}
