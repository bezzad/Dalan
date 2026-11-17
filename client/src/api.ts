// Calls to the server. Same origin only; the session is a cookie the browser sends by itself.
import { createCaller } from "./caller.ts";
import type { Message, Page } from "./messages.ts";

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
  max_caption_length: number;
  max_file_size_mb: number;
  sync_page_size: number;
  reconnect_min_seconds: number;
  reconnect_max_seconds: number;
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
  bots: async () => (await call<{ bots: Bot[] }>("GET", "/api/bots")).bots,
  findBot: (username: string) =>
    call<Bot & { started: boolean }>("GET", `/api/bots/by-username/${encodeURIComponent(username)}`),
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
  commands: async (botId: number) =>
    (await call<{ commands: Command[] }>("GET", `/api/bots/${botId}/commands`)).commands,
};
