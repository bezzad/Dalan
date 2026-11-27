// CLI-77, CLI-78: the sections of the settings area, which address opens what, the login methods and the user menu.
// Pure, so tests run without a browser.
import type { GlyphName } from "../icon.ts";
import type { TextKey } from "../i18n.ts";
import { keysShown, type BuilderState } from "../panels.ts";

export type SectionId = "profile" | "security" | "notifications" | "preferences" | "builder" | "keys";
export type GroupId = "account" | "building";

export interface Section {
  id: SectionId;
  group: GroupId;
  glyph: GlyphName;
  title: TextKey;
  help: TextKey;
}

export interface SettingsUser {
  builder: BuilderState;
  admin: boolean;
  has_phone: boolean;
  has_password: boolean;
  has_github: boolean;
  email: string | null;
}

/** Every section, in menu order. */
export const sections: readonly Section[] = [
  { id: "profile", group: "account", glyph: "profile", title: "sectionProfile", help: "sectionProfileHelp" },
  { id: "security", group: "account", glyph: "shield", title: "sectionSecurity", help: "sectionSecurityHelp" },
  { id: "notifications", group: "account", glyph: "bell", title: "notifications", help: "sectionNotificationsHelp" },
  { id: "preferences", group: "account", glyph: "sliders", title: "sectionPreferences", help: "sectionPreferencesHelp" },
  { id: "builder", group: "building", glyph: "wrench", title: "sectionBuilder", help: "sectionBuilderHelp" },
  { id: "keys", group: "building", glyph: "key", title: "sectionKeys", help: "aiAgentsNote" },
];

const groupTitles: Record<GroupId, TextKey> = { account: "groupAccount", building: "groupBuilding" };

export const settingsPath = "/account";

/** The address of a section: `/account/profile`. */
export function sectionPath(id: SectionId): string {
  return `${settingsPath}/${id}`;
}

/** Every address of the settings area (the server serves exactly these, CLI-01). */
export const settingsPaths: readonly string[] = [settingsPath, ...sections.map(section => sectionPath(section.id))];

/** CLI-77: the Building group is for an approved builder or an admin only. */
function visible(section: Section, user: SettingsUser): boolean {
  // CLI-23: every user sees the Builder section (ask, wait or open); the keys only a builder or an admin (CLI-30).
  return section.id !== "keys" || keysShown(user);
}

/** The side menu: groups with their sections. A group with no section is left out. */
export function settingsMenu(user: SettingsUser): { group: GroupId; title: TextKey; sections: Section[] }[] {
  return (["account", "building"] as const)
    .map(group => ({ group, title: groupTitles[group], sections: sections.filter(section => section.group === group && visible(section, user)) }))
    .filter(entry => entry.sections.length > 0);
}

/**
 * CLI-77: what an address opens. `/account` is Profile on a wide screen and the menu on a phone; a section the user may not
 * see (Building for a non-builder) opens Profile. `?merge` and `?link_error` (the way back from GitHub, which still names
 * `/account`) open Login and security. `path` is the address to show, `section` null means the menu list.
 */
export function settingsTarget(path: string, search: string, user: SettingsUser, wide: boolean): { section: SectionId | null; path: string } {
  const found = sections.find(section => sectionPath(section.id) === path);
  if (found) return visible(found, user) ? { section: found.id, path } : { section: "profile", path: sectionPath("profile") };
  const query = new URLSearchParams(search);
  if (query.has("merge") || query.has("link_error")) return { section: "security", path: sectionPath("security") };
  return wide ? { section: "profile", path: sectionPath("profile") } : { section: null, path: settingsPath };
}

/** CLI-77: a row's button is primary only while its value differs from the saved one. */
export function changedValue(saved: string, now: string): boolean {
  return saved.trim() !== now.trim();
}

export type MethodId = "password" | "phone" | "email" | "github";
export type MethodAction = "setUp" | "change" | "link";

export interface LoginMethod {
  id: MethodId;
  glyph: GlyphName;
  connected: boolean;
  /** The one action of the card; null when there is nothing to do (a phone or email that is set, a linked GitHub). */
  action: MethodAction | null;
  /** What the method holds, when the client knows it (the email address). */
  detail: string | null;
}

/** CLI-77: one card per login way. GitHub is listed only when the server has it on, or the user already linked it. */
export function loginMethods(user: SettingsUser, githubLogin: boolean): LoginMethod[] {
  const methods: LoginMethod[] = [
    { id: "password", glyph: "lock", connected: user.has_password, action: user.has_password ? "change" : "setUp", detail: null },
    { id: "phone", glyph: "phone", connected: user.has_phone, action: user.has_phone ? null : "setUp", detail: null },
    { id: "email", glyph: "mail", connected: user.email !== null, action: user.email !== null ? null : "setUp", detail: user.email },
  ];
  if (githubLogin || user.has_github) methods.push({ id: "github", glyph: "github", connected: user.has_github, action: user.has_github ? null : "link", detail: null });
  return methods;
}

export interface MenuItem {
  id: "myBots" | "chats" | "myApps" | "settings" | "admin" | "logout";
  glyph: GlyphName;
  text: TextKey;
  /** Null for Log out, which is an action. */
  path: string | null;
}

/** CLI-78: the user menu groups: (My bots, Chats, My apps), (Settings, and Admin for an admin), (Log out). */
export function userMenuGroups(user: { admin: boolean }): MenuItem[][] {
  return [
    [
      { id: "myBots", glyph: "bots", text: "myBots", path: "/bots" },
      { id: "chats", glyph: "chats", text: "chats", path: "/chats" },
      { id: "myApps", glyph: "apps", text: "myApps", path: "/apps" },
    ],
    [
      { id: "settings", glyph: "settings", text: "settings", path: settingsPath },
      ...(user.admin ? [{ id: "admin", glyph: "shield", text: "adminScreen", path: "/admin" } satisfies MenuItem] : []),
    ],
    [{ id: "logout", glyph: "logout", text: "logout", path: null }],
  ];
}

/** CLI-78: the grey second line under the name: the phone or the email. The client knows only the email. */
export function menuSecondLine(user: { email: string | null }): string | null {
  return user.email;
}
