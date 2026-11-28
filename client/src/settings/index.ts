// CLI-81: the dashboard: every signed-in page but a bot's chat, in one frame with a side menu in groups (CLI-77's frame).
// Workspace: Chats, My bots, My apps. Account and Building: the settings sections. Admin: only for an admin, and only once the
// admin has the second factor (SEC-15). Each page is at its own address under /dashboard (CLI-51). Below 760px there is no
// side menu: "/dashboard" is the menu as a list and a page starts with "‹ Dashboard". docs/UI.md, part "Dashboard".
import { adminPage, type AdminSection } from "../admin.ts";
import { appsScreen } from "../apps.ts";
import type { App, Screen } from "../app.ts";
import { botsScreen } from "../bots.ts";
import { botPageScreen, myBotsScreen, newBotScreen } from "../builder.ts";
import { h } from "../dom.ts";
import { glyph } from "../icon.ts";
import { builderSection, keysSection } from "./building.ts";
import { dashboardMenu, dashboardPath, dashboardTarget, sectionPath, sections, type SectionId } from "./model.ts";
import { notificationsSection } from "./notifications.ts";
import { preferencesSection } from "./preferences.ts";
import { profileSection } from "./profile.ts";
import { securitySection } from "./security.ts";
import { totpCard } from "./totpCard.ts";

type AccountSection = Extract<SectionId, "profile" | "security" | "notifications" | "preferences" | "builder" | "keys">;

/** The rows of each settings section. Adding one is an entry here and one in `sections` (model.ts). */
const content: Record<AccountSection, (app: App) => HTMLElement> = {
  profile: profileSection,
  security: securitySection,
  notifications: notificationsSection,
  preferences: preferencesSection,
  builder: builderSection,
  keys: keysSection,
};

const workspace = new Set<SectionId>(["chats", "bots", "apps"]);
const admin = new Set<SectionId>(["overview", "requests", "users", "adminBots", "showcase", "messages", "adminSettings", "access", "audit", "backup"]);

/** A workspace page is the screen it always was (its own title row, its live updates), now inside the frame. */
function workspaceScreen(app: App, section: SectionId, sub: string | undefined): Screen {
  if (section === "chats") return botsScreen(app);
  if (section === "apps") return appsScreen(app);
  if (sub === "new") return newBotScreen(app);
  return sub ? botPageScreen(app, sub) : myBotsScreen(app);
}

export function dashboardScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const wide = matchMedia("(min-width: 760px)").matches;
  const target = dashboardTarget(location.pathname, location.search, app.user, wide);
  // A way back from GitHub keeps its query: Login and security reads it (and then cleans the address).
  if (target.path !== location.pathname) history.replaceState(null, "", target.path + (target.section === "security" ? location.search : ""));
  const current = sections.find(section => section.id === target.section) ?? null;

  const link = (path: string, ...children: (Node | string)[]) => h("a", {
    href: path,
    ...(current && path === sectionPath(current.id) ? { "aria-current": "page" } : {}),
    onclick: (event: Event) => {
      event.preventDefault();
      app.go(path);
    },
  }, ...children);

  const menu = h("nav", { class: "side", "aria-label": t("dashboard") },
    ...dashboardMenu(app.user).flatMap(group => [
      h("span", { class: "label" }, t(group.title)),
      ...group.sections.map(section => link(sectionPath(section.id), glyph(section.glyph), h("span", { class: "name" }, t(section.title)), glyph("chevron")))]));

  // A workspace page keeps its own screen, so its live updates (new messages, button answers) still reach it.
  const inner = current && workspace.has(current.id) ? workspaceScreen(app, current.id, target.sub) : null;

  function pageBody(section: SectionId): HTMLElement {
    // SEC-15: an admin without the second factor sees only its set-up; the server refuses every admin call meanwhile.
    if (admin.has(section)) return app.user.totp ? adminPage(app, section as AdminSection, target.sub) : h("div", { class: "column" },
      h("p", { class: "note" }, t("adminNeeds2fa")), totpCard(app));
    return content[section as AccountSection](app);
  }

  const page = target.notFound
    // CLI-81: an admin address for anyone else, or an address that does not exist: the same "Not found", no admin data.
    ? h("section", { class: "page" }, h("h2", { class: "head" }, t("notFoundTitle")), h("p", { class: "note" }, t("notFoundText")))
    : inner ? h("section", { class: "page workspace" }, inner.element)
    : current && h("section", { class: "page" },
      h("nav", { class: "crumbs", "aria-label": "breadcrumb" }, link(dashboardPath, t("dashboard")), h("span", { "aria-hidden": "true" }, " › "), t(current.title)),
      h("h2", { class: "head" }, t(current.title)),
      ...(current.help ? [h("p", { class: "note" }, t(current.help))] : []),
      pageBody(current.id));

  const open = page !== null;
  const element = h("main", { class: open ? "wide settings dashboard" : "wide settings dashboard menu-only" },
    h("header", {},
      ...(open ? [h("a", { class: "to-menu", href: dashboardPath, onclick: (event: Event) => {
        event.preventDefault();
        app.go(dashboardPath);
      } }, "‹ ", t("dashboard"))] : []),
      h("h1", {}, t("dashboard"))),
    h("div", { class: "scroller" }, h("div", { class: "settings-body" }, menu, ...(page ? [page] : []))));
  return { ...inner, element };
}
