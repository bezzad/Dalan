// PWA entry point: who is logged in, which screen is open, and the live stream.
import { accountScreen } from "./account.ts";
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import { adminScreen } from "./admin.ts";
import { loginErrorText, type App, type Panel, type Screen } from "./app.ts";
import { Backoff } from "./backoff.ts";
import { botsScreen } from "./bots.ts";
import { builderScreen } from "./builder.ts";
import { chatScreen } from "./chat.ts";
import { Language, type TextKey } from "./i18n.ts";
import { Live } from "./live.ts";
import { loginScreen } from "./login.ts";
import { pageFor } from "./pages.ts";
import { publicScreen, type PublicPage } from "./public.ts";
import { turnOff } from "./push.ts";
import { Chats } from "./messages.ts";
import { Theme } from "./theme.ts";

const language = new Language(document.documentElement, localStorage);
const theme = new Theme(document.documentElement, localStorage, matchMedia("(prefers-color-scheme: dark)").matches);
let app: App | null = null;
let live: Live | null = null;
let screen: Screen | null = null;
let panel: Panel | null = null;
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

function showLogin(error: TextKey | null = null): void {
  live?.stop();
  app = live = screen = null;
  panel = null;
  authSettings().then(settings => show(loginScreen(language, settings, user => void enter(user).catch(fail), () => {
    language.toggle();
    showLogin();
  }, () => navigate("/"), error)), () => document.body.classList.add("offline"));
}

/** Goes to an address of the site without a page load. */
function navigate(path: string): void {
  panel = null;
  history.pushState(null, "", path);
  render();
}

function render(): void {
  if (app) route();
  else guest();
}

/** CLI-26: a visitor gets the public pages; a bot address asks for login first and opens that chat after it. */
function guest(): void {
  // CLI-36: the GitHub login came back with an error. Show it once, and clean the address.
  const error = loginErrorText(location.search);
  if (error) {
    history.replaceState(null, "", location.pathname);
    return showLogin(error);
  }
  const page = pageFor(location.pathname, false);
  if (page === "login") return showLogin();
  show(publicPage(page as PublicPage, false));
}

function publicPage(page: PublicPage, loggedIn: boolean): HTMLElement {
  return publicScreen(page, {
    language,
    theme,
    loggedIn,
    go: navigate,
    enter: loggedIn ? () => navigate("/") : showLogin,
    onLanguage() {
      language.toggle();
      render();
    },
  });
}

/** Draws the screen of the current address: "/" is the bot list, "/@name" is a chat, and the public pages. */
function route(): void {
  if (!app) return;
  const page = pageFor(location.pathname, true);
  const username = /^\/@([^/]+)$/.exec(decodeURIComponent(location.pathname))?.[1];
  screen = page === "privacy" || page === "faq" || page === "showcase" ? { element: publicPage(page, true) }
    : username ? chatScreen(app, username)
    : panel === "account" ? accountScreen(app)
    : panel === "builder" ? builderScreen(app)
    : panel === "admin" ? adminScreen(app)
    : botsScreen(app, () => {
      language.toggle();
      route();
    });
  show(screen.element);
}

async function enter(user: User): Promise<void> {
  const settings = await api.settings();
  app = {
    language,
    theme,
    user,
    auth: await authSettings(),
    chats: new Chats(api.messages, settings.sync_page_size),
    maxTextLength: settings.max_text_length,
    maxFileSizeMb: settings.max_file_size_mb,
    statusRefreshSeconds: settings.status_refresh_seconds,
    go: navigate,
    showPanel(next) {
      panel = next;
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
  if (error instanceof ApiError && error.status === 401) {
    if (app) showLogin();
    else guest();
  }
  else document.body.classList.add("offline");
}

window.addEventListener("popstate", render);
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
