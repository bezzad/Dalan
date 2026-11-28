// CLI-81 (and CLI-77, CLI-78): the dashboard: its sections in groups, which address opens what, the old addresses that move
// into it, the login methods and the user menu. Pure, so tests run without a browser.
import type { GlyphName } from "../icon.ts";
import type { TextKey } from "../i18n.ts";
import { builderView, keysShown, type BuilderState } from "../panels.ts";

export type SectionId =
  | "chats" | "bots" | "apps"
  | "profile" | "security" | "notifications" | "preferences" | "builder" | "keys"
  | "overview" | "requests" | "users" | "adminBots" | "showcase" | "messages" | "adminSettings" | "access" | "audit" | "backup";
export type GroupId = "workspace" | "account" | "building" | "admin";

export interface Section {
  id: SectionId;
  group: GroupId;
  glyph: GlyphName;
  title: TextKey;
  /** The line under the title; the workspace pages have their own title row instead. */
  help: TextKey | null;
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
  { id: "chats", group: "workspace", glyph: "chats", title: "chats", help: null },
  { id: "bots", group: "workspace", glyph: "bots", title: "myBots", help: null },
  { id: "apps", group: "workspace", glyph: "apps", title: "myApps", help: null },
  { id: "profile", group: "account", glyph: "profile", title: "sectionProfile", help: "sectionProfileHelp" },
  { id: "security", group: "account", glyph: "shield", title: "sectionSecurity", help: "sectionSecurityHelp" },
  { id: "notifications", group: "account", glyph: "bell", title: "notifications", help: "sectionNotificationsHelp" },
  { id: "preferences", group: "account", glyph: "sliders", title: "sectionPreferences", help: "sectionPreferencesHelp" },
  { id: "builder", group: "building", glyph: "wrench", title: "sectionBuilder", help: "sectionBuilderHelp" },
  { id: "keys", group: "building", glyph: "key", title: "sectionKeys", help: "aiAgentsNote" },
  { id: "overview", group: "admin", glyph: "grid", title: "adminOverview", help: "adminOverviewHelp" },
  { id: "requests", group: "admin", glyph: "inbox", title: "tabRequests", help: "adminRequestsHelp" },
  { id: "users", group: "admin", glyph: "users", title: "tabUsers", help: "adminUsersHelp" },
  { id: "adminBots", group: "admin", glyph: "bots", title: "tabBots", help: "adminBotsHelp" },
  { id: "showcase", group: "admin", glyph: "star", title: "showcase", help: "adminShowcaseHelp" },
  { id: "messages", group: "admin", glyph: "mail", title: "tabMessages", help: "adminMessagesHelp" },
  { id: "adminSettings", group: "admin", glyph: "settings", title: "tabSettings", help: "adminSettingsHelp" },
  { id: "access", group: "admin", glyph: "list", title: "tabAccess", help: "adminAccessHelp" },
  { id: "audit", group: "admin", glyph: "list", title: "tabAudit", help: "adminAuditHelp" },
  { id: "backup", group: "admin", glyph: "archive", title: "tabBackup", help: "adminBackupHelp" },
];

const groupTitles: Record<GroupId, TextKey> = { workspace: "groupWorkspace", account: "groupAccount", building: "groupBuilding", admin: "groupAdmin" };

export const dashboardPath = "/dashboard";

/** The last part of each admin address; the overview is `/dashboard/admin` itself. */
const adminSlugs: Partial<Record<SectionId, string>> = {
  requests: "requests", users: "users", adminBots: "bots", showcase: "showcase", messages: "messages", adminSettings: "settings", access: "access", audit: "audit", backup: "backup",
};

/** The address of a section: `/dashboard/profile`, `/dashboard/chats`, `/dashboard/admin/settings`. */
export function sectionPath(id: SectionId): string {
  if (id === "overview") return `${dashboardPath}/admin`;
  const slug = adminSlugs[id];
  return slug ? `${dashboardPath}/admin/${slug}` : `${dashboardPath}/${id}`;
}

const isAdminSection = (section: Section) => section.group === "admin";

/** CLI-93: the address of one user's or one bot's page in the admin dashboard. */
export function adminDetailPath(section: "users" | "adminBots", id: number): string {
  return `${sectionPath(section)}/${id}`;
}

/** CLI-81: what a user may see. The menu only hides; the server still refuses (SEC-14). */
function visible(section: Section, user: SettingsUser): boolean {
  if (isAdminSection(section)) return user.admin;
  // CLI-81: My bots and My apps for a builder or an admin. CLI-23: every user sees the Builder section; the keys only a builder (CLI-30).
  if (section.id === "bots" || section.id === "apps") return builderView(user) === "bots";
  return section.id !== "keys" || keysShown(user);
}

/** The side menu: groups with their sections. A group with no section is left out. */
export function dashboardMenu(user: SettingsUser): { group: GroupId; title: TextKey; sections: Section[] }[] {
  return (["workspace", "account", "building", "admin"] as const)
    .map(group => ({ group, title: groupTitles[group], sections: sections.filter(section => section.group === group && visible(section, user)) }))
    .filter(entry => entry.sections.length > 0);
}

export interface Target {
  /** The open section; null shows the menu as a list (a phone at `/dashboard`). */
  section: SectionId | null;
  /** The address to show. */
  path: string;
  /** CLI-81: an admin address for a user who is not an admin, or an address that does not exist. */
  notFound?: boolean;
  /** Inside My bots: "new" or a bot's username (CLI-49); in the admin Users or Bots: the id (CLI-93). */
  sub?: string;
}

/**
 * CLI-81: what an address opens. `/dashboard` is Chats on a wide screen and the menu on a phone. My bots or My apps for
 * someone who is not a builder opens Builder (CLI-23); the keys open Profile; an admin section is "Not found". `?merge` and `?link_error` (the way back from GitHub)
 * open Login and security.
 */
export function dashboardTarget(path: string, search: string, user: SettingsUser, wide: boolean): Target {
  // CLI-23: someone who is not a builder yet is sent where they can ask to become one.
  const toBuilder: Target = { section: "builder", path: sectionPath("builder") };
  const found = sections.find(section => sectionPath(section.id) === path);
  if (found) {
    if (visible(found, user)) return { section: found.id, path };
    if (isAdminSection(found)) return { section: null, path, notFound: true };
    return found.group === "workspace" ? toBuilder : { section: "profile", path: sectionPath("profile") };
  }
  // CLI-93: a user's or a bot's page in the admin dashboard; the same "Not found" as every admin address for anyone else.
  const detail = /^\/dashboard\/admin\/(users|bots)\/([1-9][0-9]*)$/.exec(path);
  if (detail) return user.admin ? { section: detail[1] === "users" ? "users" : "adminBots", path, sub: detail[2]! } : { section: null, path, notFound: true };
  const bot = /^\/dashboard\/bots\/(new|[A-Za-z][A-Za-z0-9_]*)$/.exec(path)?.[1];
  if (bot) return builderView(user) === "bots" ? { section: "bots", path, sub: bot } : toBuilder;
  const query = new URLSearchParams(search);
  if (query.has("merge") || query.has("link_error")) return { section: "security", path: sectionPath("security") };
  if (path === dashboardPath) return wide ? { section: "chats", path: sectionPath("chats") } : { section: null, path };
  return { section: null, path, notFound: true };
}

/** CLI-81: the old address of a page and its place in the dashboard; null for any other address. The query stays. */
export function dashboardAddress(path: string): string | null {
  if (path === "/chats" || path === "/apps") return `${dashboardPath}${path}`;
  if (path === "/bots" || /^\/bots\/(new|[A-Za-z][A-Za-z0-9_]*)$/.test(path)) return `${dashboardPath}${path}`;
  if (path === "/account") return dashboardPath;
  if (/^\/account\/[a-z]+$/.test(path)) return `${dashboardPath}${path.slice("/account".length)}`;
  if (path === "/admin") return sectionPath("overview");
  return null;
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
  id: "myBots" | "chats" | "myApps" | "dashboard" | "logout";
  glyph: GlyphName;
  text: TextKey;
  /** Null for Log out, which is an action. */
  path: string | null;
}

/**
 * CLI-78: the user menu groups: (My bots, Chats, My apps), (Dashboard), (Log out). Dashboard opens /dashboard for everyone
 * (owner, 2026-11-27); Settings and Admin are in its side menu.
 */
export function userMenuGroups(_user: { admin: boolean }): MenuItem[][] {
  return [
    [
      { id: "myBots", glyph: "bots", text: "myBots", path: sectionPath("bots") },
      { id: "chats", glyph: "chats", text: "chats", path: sectionPath("chats") },
      { id: "myApps", glyph: "apps", text: "myApps", path: sectionPath("apps") },
    ],
    [{ id: "dashboard", glyph: "grid", text: "dashboard", path: dashboardPath }],
    [{ id: "logout", glyph: "logout", text: "logout", path: null }],
  ];
}

/** CLI-78: the grey second line under the name: the phone or the email. The client knows only the email. */
export function menuSecondLine(user: { email: string | null }): string | null {
  return user.email;
}
