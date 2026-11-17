// PWA entry point: who is logged in, which screen is open, and the live stream.
import { accountScreen } from "./account.ts";
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import type { App, Screen } from "./app.ts";
import { Backoff } from "./backoff.ts";
import { botsScreen } from "./bots.ts";
import { chatScreen } from "./chat.ts";
import { Language } from "./i18n.ts";
import { Live } from "./live.ts";
import { loginScreen } from "./login.ts";
import { turnOff } from "./push.ts";
import { Chats } from "./messages.ts";

const language = new Language(document.documentElement, localStorage);
let app: App | null = null;
let live: Live | null = null;
let screen: Screen | null = null;
let accountOpen = false;
let auth: AuthSettings | null = null;

/** AUTH-16: read once; the login and account screens need it. */
async function authSettings(): Promise<AuthSettings> {
  return auth ??= await api.authSettings();
}

function show(element: HTMLElement): void {
  document.title = language.t("appName");
  // Shown by the style sheet while the server cannot be reached.
  document.body.dataset["connecting"] = language.t("connecting");
  document.body.replaceChildren(element);
}

function showLogin(): void {
  live?.stop();
  app = live = screen = null;
  accountOpen = false;
  authSettings().then(settings => show(loginScreen(language, settings, user => void enter(user).catch(fail), () => {
    language.toggle();
    showLogin();
  })), () => document.body.classList.add("offline"));
}

/** Draws the screen of the current address: "/" is the bot list, "/@name" is a chat. */
function route(): void {
  if (!app) return;
  const username = /^\/@([^/]+)$/.exec(decodeURIComponent(location.pathname))?.[1];
  screen = username ? chatScreen(app, username) : accountOpen ? accountScreen(app) : botsScreen(app, () => {
    language.toggle();
    route();
  });
  show(screen.element);
}

async function enter(user: User): Promise<void> {
  const settings = await api.settings();
  app = {
    language,
    user,
    auth: await authSettings(),
    chats: new Chats(api.messages, settings.sync_page_size),
    maxTextLength: settings.max_text_length,
    maxFileSizeMb: settings.max_file_size_mb,
    statusRefreshSeconds: settings.status_refresh_seconds,
    go(path) {
      accountOpen = false;
      history.pushState(null, "", path);
      route();
    },
    showAccount(open) {
      accountOpen = open;
      route();
    },
    logout() {
      // CLI-21: the next person on this browser must not get this user's notifications.
      void turnOff().catch(() => {}).then(() => api.logout()).finally(showLogin);
    },
  };

  live = new Live(new Backoff(settings.reconnect_min_seconds, settings.reconnect_max_seconds), {
    onOpen() {
      document.body.classList.remove("offline");
      // CLI-06: every time the stream opens, ask for what was missed.
      void app?.chats.sync().then(() => app?.chats.openedIds().forEach(botId => screen?.onMessage?.(botId)), fail);
    },
    onMessage(botId, message) {
      app?.chats.receive(botId, message);
      screen?.onMessage?.(botId);
    },
    onAnswer(botId, answer) {
      if (answer.text) screen?.onAnswer?.(botId, answer.text, answer.show_alert);
    },
    onRead: botId => screen?.onRead?.(botId),
    onDown: () => document.body.classList.add("offline"),
    onLoggedOut: showLogin,
  });
  live.start();
  route();
}

/** A call failed: the session has ended, or the server cannot be reached. */
function fail(error: unknown): void {
  if (error instanceof ApiError && error.status === 401) showLogin();
  else document.body.classList.add("offline");
}

window.addEventListener("popstate", route);
// Back on the network before the app could start: start again.
window.addEventListener("online", () => { if (!app) location.reload(); });

if ("serviceWorker" in navigator) {
  void navigator.serviceWorker.register("/sw.js");
  // CLI-22: a tap on a notification while the app is open goes to that chat.
  navigator.serviceWorker.addEventListener("message", event => {
    const open = (event.data as { open?: unknown } | null)?.open;
    if (typeof open === "string" && open.startsWith("/") && !open.startsWith("//")) app?.go(open);
  });
}

document.body.dataset["connecting"] = language.t("connecting");
api.me().then(enter).catch(fail);
