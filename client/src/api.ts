// Calls to the server. Same origin only; the session is a cookie the browser sends by itself.
import { createCaller } from "./caller.ts";
import type { Message, Page } from "./messages.ts";
import type { PublicNumbers, ShowcaseBot } from "./pages.ts";
import type { BuilderBot, BuilderState, NewToken, Setting } from "./panels.ts";

export { ApiError } from "./caller.ts";

export interface User {
  id: number;
  name: string;
  username: string | null;
  /** AUTH-04: null for a user who logged in only by SMS. */
  email: string | null;
  has_password: boolean;
  /** AUTH-26: false for a level 1 (email only) user, who cannot build bots yet. */
  has_phone: boolean;
  /** AUTH-18 */
  admin: boolean;
  /** AUTH-19 */
  builder: BuilderState;
  /** AUTH-30 */
  has_github: boolean;
  /** AUTH-37: the second factor (TOTP) is on. */
  totp?: boolean;
}

/** AUTH-31: one side of a merge. */
export interface MergeSide {
  id: number;
  name: string;
  bots: number;
  sites: number;
  keys: number;
  chats: number;
}

/** AUTH-31 to AUTH-33: the merge question. */
export interface MergePlan {
  way: "phone" | "email" | "github";
  keep: "this" | "other";
  this: MergeSide;
  other: MergeSide;
  /** SEC-15: "admin" when one side is an admin. */
  refusal: "both_have_products" | "merge_conflict" | "suspended" | "admin" | null;
  field: "phone" | "email" | "github" | null;
}

/** ADM-11 */
export interface AdminUser {
  id: number;
  phone: string | null;
  email: string | null;
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

/** ADM-13: an entry that waits for review. */
/** CLI-82: the counts of the admin overview (ADM-19). */
export interface AdminOverview {
  users: number;
  users_new_7d: number;
  bots: number;
  bots_online: number;
  builder_requests: number;
  showcase_requests: number;
  unread_messages: number;
  sites_online: number;
}

/** AUTH-37: a login that needs the second factor answers this instead of the user; `totpLogin` finishes it. */
export interface TotpStep {
  totp_required: true;
  ticket: string;
}

/** AUTH-37: what a login answers: the user, or the second-factor step. */
export type LoginAnswer = User | TotpStep;

export interface ShowcaseRequest {
  bot_id: number;
  username: string;
  name: string;
  owner_user_id: number;
  suspended: boolean;
  description: string;
  category: string;
  icon: boolean;
  updated_at: string;
}

/** ADM-10 */
export interface AccessRow {
  id: number;
  actor: "user" | "bot" | "phone" | "email";
  actor_id: number | null;
  phone: string | null;
  email: string | null;
  address: string;
  port: number | null;
  at: string;
}

/** ADM-17 */
export interface ContactMessage {
  id: number;
  user_id: number | null;
  email: string;
  text: string;
  address: string;
  port: number | null;
  at: string;
  read: boolean;
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
  /** CLI-33 */
  email_login: boolean;
  /** CLI-36 */
  github_login: boolean;
  password_min_length: number;
  name_max_length: number;
}

export interface Bot {
  id: number;
  username: string;
  name: string;
  /** REL-51: 0 with no icon, higher after each change. */
  icon_version: number;
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
  /** REL-51 */
  max_bot_icon_kb: number;
  /** CLI-53 */
  press_wait_seconds: number;
  /** REL-57: true when HTML files can open in the app (CLI-90). */
  file_view?: boolean;
}

/** SITE-16: one site of "My apps". */
export interface Site {
  id: number;
  name: string;
  url: string;
  online: boolean;
}

/** SITE-16: the sites, the sites domain (empty: sites are off) and the agent's server. */
export interface MySites {
  domain: string;
  scheme: string;
  tunnel_url: string;
  sites: Site[];
}

/** AUTH-21: an access key; the key itself comes only once, from makeKey. */
export interface AccessKey {
  id: number;
  name: string;
  created_at: string;
  expires_at: string;
}

export interface Command {
  command: string;
  description: string;
}

const call = createCaller((path, init) => fetch(path, init));

/** REL-52: a file that replies says so in a header. */
const replyHeader = (replyTo?: number): Record<string, string> => replyTo === undefined ? {} : { "X-Reply-To": String(replyTo) };

export const api = {
  me: () => call<User>("GET", "/api/me"),
  authSettings: () => call<AuthSettings>("GET", "/api/auth/settings"),
  devLogin: (phone: string, name: string) => call<LoginAnswer>("POST", "/api/dev/login", { phone, name }),
  /** SEC-06: for a phone or an email. */
  challenge: (target: { phone: string } | { email: string }) =>
    call<{ challenge: string; difficulty: number }>("POST", "/api/auth/challenge", target),
  sendCode: (phone: string, challenge: string, nonce: string) =>
    call<{ expires_in: number; retry_in: number; code_digits: number }>("POST", "/api/auth/sms", { phone, challenge, nonce }),
  verifyCode: (phone: string, code: string, name?: string) => call<LoginAnswer>("POST", "/api/auth/sms/verify", { phone, code, name }),
  /** AUTH-24, AUTH-25 */
  sendEmailCode: (email: string, challenge: string, nonce: string) =>
    call<{ expires_in: number; retry_in: number; code_digits: number }>("POST", "/api/auth/email", { email, challenge, nonce }),
  verifyEmailCode: (email: string, code: string, name?: string) =>
    call<LoginAnswer>("POST", "/api/auth/email/verify", { email, code, name }),
  /** AUTH-26: the code comes from sendCode. */
  addPhone: (phone: string, code: string) => call<User>("POST", "/api/account/phone", { phone, code }),
  /** AUTH-30: the code comes from sendEmailCode. */
  addEmail: (email: string, code: string) => call<User>("POST", "/api/account/email", { email, code }),
  /** AUTH-31 */
  mergePlan: () => call<MergePlan>("GET", "/api/account/merge"),
  /** AUTH-34: the answer is the kept account. */
  merge: () => call<User>("POST", "/api/account/merge"),
  dropMerge: () => call<null>("DELETE", "/api/account/merge"),
  passwordLogin: (login: string, password: string) => call<LoginAnswer>("POST", "/api/auth/password", { login, password }),
  /** AUTH-37: the second step of a login that answered `totp_required`; `code` is a TOTP or a recovery code. */
  totpLogin: (ticket: string, code: string) => call<User>("POST", "/api/auth/totp", { ticket, code }),
  /** AUTH-37: starts the set-up: the secret, its otpauth address and its QR (SVG). */
  totpStart: () => call<{ secret: string; otpauth_url: string; qr_svg: string }>("POST", "/api/account/totp"),
  totpConfirm: (code: string) => call<{ recovery_codes: string[] }>("POST", "/api/account/totp/confirm", { code }),
  totpOff: (code: string) => call<null>("POST", "/api/account/totp/off", { code }),
  setPassword: (password: string, current?: string) =>
    call<null>("POST", "/api/account/password", { password, current_password: current }),
  setUsername: (username: string) => call<User>("POST", "/api/account/username", { username }),
  logout: () => call<null>("POST", "/api/logout"),
  settings: () => call<Settings>("GET", "/api/settings"),
  bots: async () => (await call<{ bots: ListedBot[] }>("GET", "/api/bots")).bots,
  findBot: (username: string) =>
    call<Bot & { started: boolean; suspended: boolean }>("GET", `/api/bots/by-username/${encodeURIComponent(username)}`),
  /** REL-15; `replyTo` (REL-52) is the message it replies to. */
  send: (botId: number, text: string, replyTo?: number) =>
    call<Message>("POST", `/api/bots/${botId}/messages`, replyTo === undefined ? { text } : { text, reply_to: replyTo }),
  messages: (botId: number, range: { after?: number; before?: number }) => {
    const query = new URLSearchParams(Object.entries(range).map(([name, value]) => [name, String(value)]));
    return call<Page>("GET", `/api/bots/${botId}/messages?${query}`);
  },
  /** REL-28: the file is the body; its name and the caption go percent-encoded in headers. */
  sendFile: (botId: number, file: File, caption: string, replyTo?: number) => call<Message>("POST", `/api/bots/${botId}/files`, file,
    { "X-File-Name": encodeURIComponent(file.name), "X-Caption": encodeURIComponent(caption), ...replyHeader(replyTo) }),
  /** REL-28 and CLI-46: a recorded voice with its length in seconds. */
  sendVoice: (botId: number, voice: Blob, name: string, seconds: number, replyTo?: number) => call<Message>("POST", `/api/bots/${botId}/files`, voice,
    { "X-File-Name": encodeURIComponent(name), "X-Voice-Duration": String(seconds), ...replyHeader(replyTo) }),
  /** REL-51 */
  setBotIcon: (botId: number, image: Blob) => call<unknown>("PUT", `/api/builder/bots/${botId}/icon`, image),
  removeBotIcon: (botId: number) => call<unknown>("DELETE", `/api/builder/bots/${botId}/icon`),
  /** REL-29: where the browser loads a file of the chat. */
  fileUrl: (botId: number, fileId: string) => `/api/bots/${botId}/files/${encodeURIComponent(fileId)}`,
  /** REL-57: a short-lived link to an HTML file on the view host (CLI-90). */
  fileViewLink: (botId: number, fileId: string) =>
    call<{ url: string; expires_at: string }>("POST", `/api/bots/${botId}/files/${encodeURIComponent(fileId)}/view`),
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
  mySites: () => call<MySites>("GET", "/api/sites"),
  deleteSite: (siteId: number) => call<null>("DELETE", `/api/sites/${siteId}`),
  reserveSite: (name: string) => call<Site>("POST", "/api/sites", { name }),
  accessKeys: async () => (await call<{ keys: AccessKey[] }>("GET", "/api/account/keys")).keys,
  makeKey: (name: string) => call<AccessKey & { key: string }>("POST", "/api/account/keys", { name }),
  deleteKey: (keyId: number) => call<null>("DELETE", `/api/account/keys/${keyId}`),
  rotateToken: (botId: number) => call<NewToken>("POST", `/api/builder/bots/${botId}/token`),
  renameBot: (botId: number, name: string) => call<BuilderBot>("PATCH", `/api/builder/bots/${botId}`, { name }),
  deleteBot: (botId: number) => call<{ id: number; deleted: boolean }>("DELETE", `/api/builder/bots/${botId}`),
  /** CLI-82 */
  adminOverview: () => call<AdminOverview>("GET", "/api/admin/overview"),
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
  /** ADM-14 */
  /** CLI-72: the live version and commit. */
  publicSettings: () => call<PublicNumbers>("GET", "/api/public/settings"),
  /** ADM-17 */
  contact: (email: string, text: string) => call<{ id: number }>("POST", "/api/contact", { email, text }),
  contactMessages: async (before?: number) =>
    (await call<{ rows: ContactMessage[] }>("GET", `/api/admin/contact${before ? `?before=${before}` : ""}`)).rows,
  markMessage: (id: number, read: boolean) => call<ContactMessage>("POST", `/api/admin/contact/${id}/read`, { read }),
  /** REL-43 */
  showcase: (category: string, after?: number) => {
    const query = new URLSearchParams({ ...(category ? { category } : {}), ...(after ? { after: String(after) } : {}) });
    return call<{ categories: string[]; bots: ShowcaseBot[] }>("GET", `/api/showcase?${query}`);
  },
  /** REL-40 to REL-42 */
  askShowcase: (botId: number, description: string, category: string) =>
    call<BuilderBot["showcase"]>("PUT", `/api/builder/bots/${botId}/showcase`, { description, category }),
  ownIconUrl: (botId: number) => `/api/builder/bots/${botId}/showcase/icon`,
  leaveShowcase: (botId: number) => call<null>("DELETE", `/api/builder/bots/${botId}/showcase`),
  /** ADM-13 */
  showcaseRequests: async () => (await call<{ entries: ShowcaseRequest[] }>("GET", "/api/admin/showcase")).entries,
  reviewShowcase: (botId: number, approve: boolean) => call<null>("POST", `/api/admin/showcase/${botId}`, { approve }),
  commands: async (botId: number) =>
    (await call<{ commands: Command[] }>("GET", `/api/bots/${botId}/commands`)).commands,
};
