// CLI-77: the settings area. A side menu in groups, and one section per page at its own address (CLI-51). Each section is a
// function that returns its rows (profile.ts, security.ts, ...). Below 760px there is no side menu: "/account" is the menu
// as a list and a section page starts with "‹ Settings". docs/UI.md, part "Settings".
import type { App, Screen } from "../app.ts";
import { h } from "../dom.ts";
import { glyph } from "../icon.ts";
import { builderSection, keysSection } from "./building.ts";
import { notificationsSection } from "./notifications.ts";
import { preferencesSection } from "./preferences.ts";
import { profileSection } from "./profile.ts";
import { securitySection } from "./security.ts";
import { sectionPath, sections, settingsMenu, settingsPath, settingsTarget, type SectionId } from "./model.ts";

/** The rows of each section. Adding a section is one entry here and one in `sections` (model.ts). */
const content: Record<SectionId, (app: App) => HTMLElement> = {
  profile: profileSection,
  security: securitySection,
  notifications: notificationsSection,
  preferences: preferencesSection,
  builder: builderSection,
  keys: keysSection,
};

export function settingsScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const wide = matchMedia("(min-width: 760px)").matches;
  const target = settingsTarget(location.pathname, location.search, app.user, wide);
  // A way back from GitHub keeps its query: Login and security reads it (and then cleans the address).
  if (target.path !== location.pathname) history.replaceState(null, "", target.path + (target.section === "security" ? location.search : ""));
  const current = sections.find(section => section.id === target.section) ?? null;

  const link = (path: string, ...children: (Node | string)[]) => h("a", {
    href: path,
    ...(path === (current ? sectionPath(current.id) : null) ? { "aria-current": "page" } : {}),
    onclick: (event: Event) => {
      event.preventDefault();
      app.go(path);
    },
  }, ...children);

  const menu = h("nav", { class: "side", "aria-label": t("settings") },
    ...settingsMenu(app.user).flatMap(group => [
      h("span", { class: "label" }, t(group.title)),
      ...group.sections.map(section => link(sectionPath(section.id), glyph(section.glyph), h("span", { class: "name" }, t(section.title)), glyph("chevron")))]));

  const page = current && h("section", { class: "page" },
    h("nav", { class: "crumbs", "aria-label": "breadcrumb" }, link(settingsPath, t("settings")), h("span", { "aria-hidden": "true" }, " › "), t(current.title)),
    h("h2", { class: "head" }, t(current.title)),
    h("p", { class: "note" }, t(current.help)),
    content[current.id](app));

  const element = h("main", { class: current ? "wide settings" : "wide settings menu-only" },
    h("header", {},
      ...(current ? [h("a", { class: "to-menu", href: settingsPath, onclick: (event: Event) => {
        event.preventDefault();
        app.go(settingsPath);
      } }, "‹ ", t("settings"))] : []),
      h("h1", {}, t("settings"))),
    h("div", { class: "scroller" }, h("div", { class: "settings-body" }, menu, ...(page ? [page] : []))));
  return { element };
}
