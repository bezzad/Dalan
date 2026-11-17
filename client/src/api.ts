// Calls to the server. Same origin only; the session is a cookie the browser sends by itself.
import type { Message } from "./messages.ts";

export interface User {
  id: number;
  name: string;
  username: string | null;
}

export interface Bot {
  id: number;
  username: string;
  name: string;
}

/** REL-18: values the client must not hard-code. */
export interface Settings {
  max_text_length: number;
  sync_page_size: number;
  reconnect_min_seconds: number;
  reconnect_max_seconds: number;
}

/** A failed call: the HTTP status and the server's stable error code ("" when there is none). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, code: string) {
    super(`${status} ${code}`);
    this.status = status;
    this.code = code;
  }
}

async function call<T>(method: "GET" | "POST", path: string, body?: unknown): Promise<T> {
  const response = await fetch(path, {
    method,
    headers: body === undefined ? {} : { "Content-Type": "application/json" },
    body: body === undefined ? null : JSON.stringify(body),
  });

  const json: unknown = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) throw new ApiError(response.status, (json as { error?: string } | null)?.error ?? "");
  return json as T;
}

export const api = {
  me: () => call<User>("GET", "/api/me"),
  devLogin: (phone: string, name: string) => call<User>("POST", "/api/dev/login", { phone, name }),
  logout: () => call<null>("POST", "/api/logout"),
  settings: () => call<Settings>("GET", "/api/settings"),
  bots: async () => (await call<{ bots: Bot[] }>("GET", "/api/bots")).bots,
  findBot: (username: string) =>
    call<Bot & { started: boolean }>("GET", `/api/bots/by-username/${encodeURIComponent(username)}`),
  send: (botId: number, text: string) => call<Message>("POST", `/api/bots/${botId}/messages`, { text }),
  messages: async (botId: number, range: { after?: number; before?: number }) => {
    const query = new URLSearchParams(Object.entries(range).map(([name, value]) => [name, String(value)]));
    return (await call<{ messages: Message[] }>("GET", `/api/bots/${botId}/messages?${query}`)).messages;
  },
};
