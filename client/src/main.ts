// PWA entry point: who is logged in, which screen is open, and the live stream.
import { ApiError, api, type User } from "./api.ts";
import type { App, Screen } from "./app.ts";
import { Backoff } from "./backoff.ts";
import { botsScreen } from "./bots.ts";
import { chatScreen } from "./chat.ts";
import { Language } from "./i18n.ts";
import { Live } from "./live.ts";
import { loginScreen } from "./login.ts";
import { Chats } from "./messages.ts";

const language = new Language(document.documentElement, localStorage);
let app: App | null = null;
let live: Live | null = null;
let screen: Screen | null = null;

function show(element: HTMLElement): void {
  document.title = language.t("appName");
  // Shown by the style sheet while the server cannot be reached.
  document.body.dataset["connecting"] = language.t("connecting");
  document.body.replaceChildren(element);
}

function showLogin(): void {
  live?.stop();
  app = live = screen = null;
  show(loginScreen(language, user => void enter(user).catch(fail), () => {
    language.toggle();
    showLogin();
  }));
}

/** Draws the screen of the current address: "/" is the bot list, "/@name" is a chat. */
function route(): void {
  if (!app) return;
  const username = /^\/@([^/]+)$/.exec(decodeURIComponent(location.pathname))?.[1];
  screen = username ? chatScreen(app, username) : botsScreen(app, () => {
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
    chats: new Chats(api.messages, settings.sync_page_size),
    maxTextLength: settings.max_text_length,
    go(path) {
      history.pushState(null, "", path);
      route();
    },
    logout() {
      void api.logout().finally(showLogin);
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

if ("serviceWorker" in navigator) void navigator.serviceWorker.register("/sw.js");

document.body.dataset["connecting"] = language.t("connecting");
api.me().then(enter).catch(fail);
