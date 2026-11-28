// PWA entry point: who is logged in, which screen is open, and the live stream.
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import { loginErrorText, type App, type Screen } from "./app.ts";
import { Backoff } from "./backoff.ts";
import { chatScreen } from "./chat.ts";
import { Connection } from "./connection.ts";
import { Drafts } from "./compose.ts";
import { closeOverlays } from "./overlays.ts";
import { Language, type TextKey } from "./i18n.ts";
import { Live } from "./live.ts";
import { loginScreen, markReturning } from "./login.ts";
import { askedNext, loginTarget, rememberNext, startsAtTop, takeNext } from "./next.ts";
import { initial, pageFor } from "./pages.ts";
import { h } from "./dom.ts";
import { glyph } from "./icon.ts";
import { toastRegion } from "./toast.ts";
import { bellMenu } from "./inbox.ts";
import { headerTools, homeMark, publicPages, publicScreen, siteBrand, type PublicPage } from "./public.ts";
import { turnOff } from "./push.ts";
import { totpTicketIn } from "./totp.ts";
import { appLinksFor, Chats, loadsAnew } from "./messages.ts";
import { dashboardScreen } from "./settings/index.ts";
import { dashboardAddress, menuSecondLine, userMenuGroups } from "./settings/model.ts";
import { Theme, themeButton } from "./theme.ts";

const language = new Language(document.documentElement, localStorage);
const theme = new Theme(document.documentElement, localStorage, matchMedia("(prefers-color-scheme: dark)").matches);
let app: App | null = null;
let live: Live | null = null;
let screen: Screen | null = null;
let auth: AuthSettings | null = null;
// CLI-75: the ring on the bot's icon in a chat, on the Dalan mark elsewhere.
const connection = new Connection(
  () => document.querySelector<HTMLElement>("header .who .face") ?? document.querySelector<HTMLElement>("header .mark"),
  () => ({ connected: language.t("connected"), connecting: language.t("connecting") }));

/** AUTH-16: read once; the login and account screens need it. */
async function authSettings(): Promise<AuthSettings> {
  return auth ??= await api.authSettings();
}

function show(element: HTMLElement): void {
  // CLI-87, CLI-89, CLI-90: no viewer or editor stays over the next screen.
  closeOverlays();
  document.title = language.t("appName");
  // CLI-55: a bot's chat links that bot's manifest and icons; every other screen links Dalan's again.
  const links = appLinksFor(location.pathname);
  document.querySelector("link[rel=manifest]")?.setAttribute("href", links.manifest);
  const icon = document.querySelector("link[rel=icon]");
  icon?.setAttribute("href", links.icon);
  if (links.iconType) icon?.setAttribute("type", links.iconType); else icon?.removeAttribute("type");
  document.querySelector("link[rel=apple-touch-icon]")?.setAttribute("href", links.appleIcon);
  document.body.replaceChildren(element, toastRegion(), connection.status);
  connection.apply();
  // An address like "/#plans": the part is drawn now, so go to it.
  if (location.hash.length > 1) document.getElementById(location.hash.slice(1))?.scrollIntoView();
}

function showLogin(error: TextKey | null = null): void {
  // CLI-88: a logout, or a session that ended (401), leaves no draft in this browser.
  Drafts.clearAll(localStorage);
  live?.stop();
  app = live = screen = null;
  connection.reset();
  authSettings().then(settings => show(loginScreen(language, settings, user => {
    // CLI-63: after login, the address the person came from; from the landing page, "My bots".
    history.replaceState(null, "", loginTarget(location.pathname, location.search, takeNext()));
    void enter(user).catch(fail);
  }, () => {
    language.toggle();
    showLogin();
  }, () => navigate("/"), error)), () => connection.setDown());
}

/** CLI-55: an installed app (or one added to the home screen) never loads the page anew. */
const standalone = () => matchMedia("(display-mode: standalone)").matches;

/** The path of the screen shown now, for the back button (CLI-55). */
let shownPath = location.pathname;

/** Goes to an address of the site without a page load; into or out of a bot's chat in a browser tab, with one (CLI-55). */
function navigate(path: string): void {
  if (loadsAnew(location.pathname, path, standalone())) {
    location.assign(path);
    return;
  }
  history.pushState(null, "", path);
  render();
  // CLI-68: a new page starts at the top; an address with a #part goes to that part (show() does it).
  if (startsAtTop(path)) window.scrollTo(0, 0);
}

function render(): void {
  // CLI-55: the back button out of (or into) a bot's chat loads that page anew, like a link does.
  if (loadsAnew(shownPath, location.pathname, standalone())) return location.reload();
  shownPath = location.pathname;
  if (app) route();
  else guest();
}

/** CLI-26: a visitor gets the public pages; a bot address asks for login first and opens that chat after it. */
function guest(): void {
  // CLI-92: back from a GitHub login that needs the second factor: the login screen asks for the code.
  if (totpTicketIn(location.search)) return showLogin();
  // CLI-36: the GitHub login came back with an error. Show it once, and clean the address.
  const error = loginErrorText(location.search);
  if (error) {
    history.replaceState(null, "", location.pathname);
    return showLogin(error);
  }
  // CLI-63: "/?next=/guide/site" asks for the login and comes back to that address; the address is kept and cleaned.
  const asked = askedNext(location.search, null);
  if (asked) {
    rememberNext(asked);
    history.replaceState(null, "", location.pathname);
    return showLogin();
  }
  // CLI-63: a path kept for a GitHub round trip that was left halfway must not steer a later login.
  takeNext();
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
  // CLI-78: the name and the phone or email, then groups with an icon per item and a line between groups.
  const second = menuSecondLine(current.user);
  const groups = userMenuGroups(current.user).flatMap((group, index) => [
    ...(index > 0 ? [h("hr", {})] : []),
    ...group.map(entry => h("button", {
      type: "button", class: "item", onclick: () => entry.path === null ? current.logout() : navigate(entry.path),
    }, glyph(entry.glyph), h("span", {}, t(entry.text)))),
  ]);
  return h("details", { class: face ? "menu user face" : "menu user" },
    h("summary", { "aria-label": current.user.name, title: current.user.name },
      face ? h("span", { class: "avatar" }, initial(current.user.name)) : h("bdi", {}, current.user.name)),
    h("div", { class: "panel" },
      h("div", { class: "who" }, h("strong", { dir: "auto" }, current.user.name), ...(second ? [h("span", { class: "note", dir: "ltr" }, second)] : [])),
      ...groups));
}

/** CLI-96: the bell of a signed-in user; drawn anew with each screen, so its count is new on each screen change. */
function bell(): HTMLElement | null {
  if (!app) return null;
  return bellMenu(language, navigate);
}

/** Language, theme, the bell and the user area: the same end of the header on every page. */
function tools(): HTMLElement {
  return headerTools(language, theme, () => {
    language.toggle();
    render();
  }, userArea(), bell());
}

function publicPage(page: PublicPage, loggedIn: boolean): HTMLElement {
  return publicScreen(page, {
    tools: tools(),
    language,
    loggedIn,
    email: app?.user.email ?? null,
    user: app?.user ?? null,
    go: navigate,
    enter: loggedIn ? () => navigate("/chats") : showLogin,
  });
}

/** CLI-51: draws the screen of the current address, and only of the address: "/" the landing page, "/dashboard/...", "/@name"... */
function route(): void {
  if (!app) return;
  // CLI-81: the old addresses (/chats, /bots, /apps, /account/..., /admin) now live in the dashboard; links to them still work.
  const moved = dashboardAddress(location.pathname);
  if (moved) history.replaceState(null, "", moved + location.search);
  const page = pageFor(location.pathname, true);
  const username = /^\/@([^/]+)$/.exec(decodeURIComponent(location.pathname))?.[1];
  screen = publicPages.includes(page)
    ? { element: publicPage(page as PublicPage, true) }
    // CLI-45: the chat page has its own header, with no site header above it.
    : username ? chatScreen(app, username, [themeButton(theme, language.t.bind(language)), bell()!, userArea(true), homeMark(language.t("appName"), navigate)])
    // CLI-81: every other signed-in page is in the dashboard.
    : dashboardScreen(app);
  // The public pages have the site header; the app screens get the same logo and name on top.
  if (!screen.element.classList.contains("public") && !username) {
    screen.element.prepend(h("header", { class: "site" }, siteBrand(language.t("appName"), navigate), tools()));
  }
  show(screen.element);
}

async function enter(user: User): Promise<void> {
  // CLI-36: the next login screen of this browser puts the fast ways first.
  markReturning();
  const settings = await api.settings();
  app = {
    language,
    theme,
    user,
    auth: await authSettings(),
    chats: new Chats(api.messages, settings.sync_page_size),
    maxTextLength: settings.max_text_length,
    maxFileSizeMb: settings.max_file_size_mb,
    fileView: settings.file_view ?? false,
    statusRefreshSeconds: settings.status_refresh_seconds,
    maxBotIconKb: settings.max_bot_icon_kb,
    pressWaitSeconds: settings.press_wait_seconds,
    go: navigate,
    refresh: render,
    logout() {
      // CLI-21: the next person on this browser must not get this user's notifications.
      void turnOff().catch(() => {}).then(() => api.logout()).finally(showLogin);
    },
  };

  live = new Live(new Backoff(settings.reconnect_min_seconds, settings.reconnect_max_seconds), {
    onOpen() {
      connection.setUp();
      // CLI-06: every time the stream opens, ask for what was missed.
      void app?.chats.sync().then(() => app?.chats.openedIds().forEach(botId => screen?.onMessage?.(botId)), fail);
    },
    onMessage(botId, message) {
      app?.chats.receive(botId, message);
      screen?.onMessage?.(botId);
    },
    onAnswer: (botId, answer) => screen?.onAnswer?.(botId, answer),
    onRead: botId => screen?.onRead?.(botId),
    onDown: () => connection.setDown(),
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
  else connection.setDown();
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

// CLI-63: back from GitHub on "/": go to the address kept before the round trip (or `?next=`).
api.me().then(user => {
  const asked = askedNext(location.search, takeNext());
  if (asked && location.pathname === "/") history.replaceState(null, "", asked);
  return enter(user);
}).catch(fail);
