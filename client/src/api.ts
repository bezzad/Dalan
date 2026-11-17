// Calls to the server. Same origin only; the session is a cookie the browser sends by itself.
import { createCaller } from "./caller.ts";
import type { Message, Page } from "./messages.ts";
import type { BuilderBot, BuilderState, NewToken, Setting } from "./panels.ts";

export { ApiError } from "./caller.ts";

export interface User {
  id: number;
  name: string;
  username: string | null;
  has_password: boolean;
  /** AUTH-18 */
  admin: boolean;
  /** AUTH-19 */
  builder: BuilderState;
}

/** ADM-11 */
export interface AdminUser {
  id: number;
  phone: string;
  name: string;
  username: string | null;
  created_at: string;
  builder: BuilderState;
  builder_requested_at: string | null;
  suspended: boolean;
}

/** ADM-12 */
export interface AdminBot {
  id: number;
  username: string;
  name: string;
  owner_user_id: number;
  created_at: string;
  last_seen_at: string | null;
  suspended: boolean;
}

/** ADM-10 */
export interface AccessRow {
  id: number;
  actor: "user" | "bot" | "phone";
  actor_id: number | null;
  phone: string | null;
  address: string;
  port: number | null;
  at: string;
}

/** ADM-09 */
export interface AuditRow {
  id: number;
  user_id: number;
  action: string;
  target: string;
  detail: string | null;
  at: string;
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

/** REL-33: is the bot's home computer on? */
export interface BotStatus {
  online: boolean;
  last_seen: string | null;
}

/** REL-14: a bot in the list, with its unread count and status. */
export type ListedBot = Bot & BotStatus & { unread: number; suspended: boolean };

/** REL-18: values the client must not hard-code. */
export interface Settings {
  max_text_length: number;
  max_caption_length: number;
  max_file_size_mb: number;
  sync_page_size: number;
  reconnect_min_seconds: number;
  reconnect_max_seconds: number;
  status_refresh_seconds: number;
}

export interface Command {
  command: string;
  description: string;
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
  bots: async () => (await call<{ bots: ListedBot[] }>("GET", "/api/bots")).bots,
  findBot: (username: string) =>
    call<Bot & { started: boolean; suspended: boolean }>("GET", `/api/bots/by-username/${encodeURIComponent(username)}`),
  send: (botId: number, text: string) => call<Message>("POST", `/api/bots/${botId}/messages`, { text }),
  messages: (botId: number, range: { after?: number; before?: number }) => {
    const query = new URLSearchParams(Object.entries(range).map(([name, value]) => [name, String(value)]));
    return call<Page>("GET", `/api/bots/${botId}/messages?${query}`);
  },
  /** REL-28: the file is the body; its name and the caption go percent-encoded in headers. */
  sendFile: (botId: number, file: File, caption: string) => call<Message>("POST", `/api/bots/${botId}/files`, file,
    { "X-File-Name": encodeURIComponent(file.name), "X-Caption": encodeURIComponent(caption) }),
  /** REL-29: where the browser loads a file of the chat. */
  fileUrl: (botId: number, fileId: string) => `/api/bots/${botId}/files/${encodeURIComponent(fileId)}`,
  press: (botId: number, messageId: number, data: string) =>
    call<{ id: string }>("POST", `/api/bots/${botId}/callbacks`, { message_id: messageId, data }),
  /** REL-32: the user read the chat up to this message. */
  read: (botId: number, messageId: number) => call<{ unread: number }>("POST", `/api/bots/${botId}/read`, { message_id: messageId }),
  status: (botId: number) => call<BotStatus>("GET", `/api/bots/${botId}/status`),
  /** REL-35: the server's push key, or null when push is off. */
  pushKey: async () => (await call<{ key: string | null }>("GET", "/api/push")).key,
  subscribe: (subscription: PushSubscriptionJSON) => call<null>("POST", "/api/push/subscribe", subscription),
  unsubscribe: (endpoint: string) => call<null>("POST", "/api/push/unsubscribe", { endpoint }),
  askBuilder: () => call<User>("POST", "/api/account/builder"),
  builderBots: async () => (await call<{ bots: BuilderBot[] }>("GET", "/api/builder/bots")).bots,
  createBot: (username: string, name: string) => call<NewToken>("POST", "/api/builder/bots", { username, name }),
  rotateToken: (botId: number) => call<NewToken>("POST", `/api/builder/bots/${botId}/token`),
  adminSettings: async () => (await call<{ settings: Setting[] }>("GET", "/api/admin/settings")).settings,
  setSetting: (key: string, value: string) => call<Setting>("PUT", `/api/admin/settings/${encodeURIComponent(key)}`, { value }),
  resetSetting: (key: string) => call<Setting>("DELETE", `/api/admin/settings/${encodeURIComponent(key)}`),
  adminUsers: async (query: string) =>
    (await call<{ users: AdminUser[] }>("GET", `/api/admin/users?${new URLSearchParams(query ? { q: query } : { builder: "requested" })}`)).users,
  setBuilder: (userId: number, approve: boolean) => call<AdminUser>("POST", `/api/admin/users/${userId}/builder`, { approve }),
  suspendUser: (userId: number, suspended: boolean) => call<AdminUser>("POST", `/api/admin/users/${userId}/suspend`, { suspended }),
  adminBots: async (query: string) =>
    (await call<{ bots: AdminBot[] }>("GET", `/api/admin/bots?${new URLSearchParams({ q: query })}`)).bots,
  suspendBot: (botId: number, suspended: boolean) => call<AdminBot>("POST", `/api/admin/bots/${botId}/suspend`, { suspended }),
  accessLog: async (filter: string, value: string, before?: number) => {
    const query = new URLSearchParams({ [filter]: value, ...(before ? { before: String(before) } : {}) });
    return (await call<{ rows: AccessRow[] }>("GET", `/api/admin/access-log?${query}`)).rows;
  },
  audit: async (before?: number) =>
    (await call<{ rows: AuditRow[] }>("GET", `/api/admin/audit${before ? `?before=${before}` : ""}`)).rows,
  commands: async (botId: number) =>
    (await call<{ commands: Command[] }>("GET", `/api/bots/${botId}/commands`)).commands,
};
