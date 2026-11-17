// Calls to the server. Same origin only; the session is a cookie the browser sends by itself.
import { createCaller } from "./caller.ts";
import type { Message } from "./messages.ts";

export { ApiError } from "./caller.ts";

export interface User {
  id: number;
  name: string;
  username: string | null;
  has_password: boolean;
}

/** AUTH-16: what the login and account screens must not hard-code. */
export interface AuthSettings {
  dev_login: boolean;
  password_min_length: number;
  name_max_length: number;
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

const call = createCaller((path, init) => fetch(path, init));

export const api = {
  me: () => call<User>("GET", "/api/me"),
  authSettings: () => call<AuthSettings>("GET", "/api/auth/settings"),
  devLogin: (phone: string, name: string) => call<User>("POST", "/api/dev/login", { phone, name }),
  challenge: (phone: string) => call<{ challenge: string; difficulty: number }>("POST", "/api/auth/challenge", { phone }),
  sendCode: (phone: string, challenge: string, nonce: string) =>
    call<{ expires_in: number; retry_in: number; code_digits: number }>("POST", "/api/auth/sms", { phone, challenge, nonce }),
  verifyCode: (phone: string, code: string, name?: string) => call<User>("POST", "/api/auth/sms/verify", { phone, code, name }),
  passwordLogin: (login: string, password: string) => call<User>("POST", "/api/auth/password", { login, password }),
  setPassword: (password: string, current?: string) =>
    call<null>("POST", "/api/account/password", { password, current_password: current }),
  setUsername: (username: string) => call<User>("POST", "/api/account/username", { username }),
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
