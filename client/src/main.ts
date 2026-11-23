// PWA entry point: who is logged in, which screen is open, and the live stream.
import { accountScreen } from "./account.ts";
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import { adminScreen } from "./admin.ts";
import { appsScreen } from "./apps.ts";
import { loginErrorText, type App, type Panel, type Screen } from "./app.ts";
import { Backoff } from "./backoff.ts";
import { botsScreen } from "./bots.ts";
import { builderScreen } from "./builder.ts";
import { chatScreen } from "./chat.ts";
import { Language, type TextKey } from "./i18n.ts";
import { Live } from "./live.ts";
import { loginScreen } from "./login.ts";
import { initial, pageFor } from "./pages.ts";
import { h } from "./dom.ts";
import { builderView } from "./panels.ts";
import { toastRegion } from "./toast.ts";
import { headerTools, homeMark, publicPages, publicScreen, siteBrand, type PublicPage } from "./public.ts";
import { turnOff } from "./push.ts";
import { Chats } from "./messages.ts";
import { Theme, themeButton } from "./theme.ts";

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
  document.body.replaceChildren(element, toastRegion());
  // An address like "/#plans": the part is drawn now, so go to it.
  if (location.hash.length > 1) document.getElementById(location.hash.slice(1))?.scrollIntoView();
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

/**
 * The end of the site header on every page: "Log in" for a visitor, the user menu after login (owner, 2026-11-20).
 * `face`: the menu shows a round initial instead of the name (the chat header, CLI-45).
 */
function userArea(face = false): HTMLElement {
  const t = language.t.bind(language);
  // On a phone the button says only "Log in", so the header stays on one line (CLI-42).
  if (!app) return h("button", { type: "button", class: "primary", "aria-label": t("enter"), onclick: () => showLogin() },
    h("span", { class: "long" }, t("enter")), h("span", { class: "short" }, t("login")));
  const current = app;
  const item = (label: string, onclick: () => void) => h("button", { type: "button", class: "item", onclick }, label);
  return h("details", { class: face ? "menu user face" : "menu user" },
    h("summary", { "aria-label": current.user.name, title: current.user.name },
      face ? h("span", { class: "avatar" }, initial(current.user.name)) : h("bdi", {}, current.user.name)),
    h("div", { class: "panel" },
      // "My bots" are the bots this user built; "Chats" the bots they talk to (owner, 2026-11-20).
      item(t("myBots"), () => current.showPanel(builderView(current.user) === "bots" ? "builder" : "account")),
      item(t("chats"), () => navigate("/bots")),
      item(t("myApps"), () => navigate("/apps")),
      item(t("account"), () => current.showPanel("account")),
      ...(current.user.admin ? [item(t("adminScreen"), () => current.showPanel("admin"))] : []),
      item(t("logout"), () => current.logout())));
}

/** Language, theme and the user area: the same end of the header on every page. */
function tools(): HTMLElement {
  return headerTools(language, theme, () => {
    language.toggle();
    render();
  }, userArea());
}

function publicPage(page: PublicPage, loggedIn: boolean): HTMLElement {
  return publicScreen(page, {
    tools: tools(),
    language,
    loggedIn,
    go: navigate,
    enter: loggedIn ? () => navigate("/bots") : showLogin,
  });
}

/** Draws the screen of the current address: "/" is the landing page, "/bots" the bot list, "/@name" a chat, and the public pages. */
function route(): void {
  if (!app) return;
  const page = pageFor(location.pathname, true);
  const username = /^\/@([^/]+)$/.exec(decodeURIComponent(location.pathname))?.[1];
  screen = publicPages.includes(page)
    ? { element: publicPage(page as PublicPage, true) }
    // CLI-45: the chat page has its own header, with no site header above it.
    : username ? chatScreen(app, username, [themeButton(theme, language.t.bind(language)), userArea(true), homeMark(language.t("appName"), navigate)])
    : page === "apps" && panel === null ? appsScreen(app)
    : panel === "account" ? accountScreen(app)
    : panel === "builder" ? builderScreen(app)
    : panel === "admin" ? adminScreen(app)
    : botsScreen(app);
  // The public pages have the site header; the app screens get the same logo and name on top.
  if (!screen.element.classList.contains("public") && !username) {
    screen.element.prepend(h("header", { class: "site" }, siteBrand(language.t("appName"), navigate), tools()));
  }
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
      // The panels live on the bot list address, so the menu can open them from any page.
      if (location.pathname !== "/bots") history.pushState(null, "", "/bots");
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
