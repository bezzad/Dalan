// Pure logic of the account, builder and admin screens (CLI-23 to CLI-25), kept apart so tests run without a browser.

import type { ShowcaseEntry } from "./pages.ts";

export type BuilderState = "none" | "requested" | "approved" | "refused";

/**
 * CLI-23: what the account screen shows about building bots. A verified phone makes a builder with no request (AUTH-39);
 * a user with no phone must verify one first (CLI-34). CLI-84: a refused user sees why. "closed": a phone that is not a
 * builder, only while the admin has turned Identity:AutoBuilder off.
 */
export function builderView(user: { builder: BuilderState; admin: boolean; has_phone: boolean }): "phone" | "refused" | "closed" | "bots" {
  if (user.admin || user.builder === "approved") return "bots";
  if (!user.has_phone) return "phone";
  return user.builder === "refused" ? "refused" : "closed";
}

/** CLI-30: the account screen shows the access keys and the ready prompt only to an approved builder or an admin. */
export function keysShown(user: { builder: BuilderState; admin: boolean; has_phone: boolean }): boolean {
  return builderView(user) === "bots";
}

export interface BuilderBot {
  id: number;
  username: string;
  name: string;
  created_at: string;
  online: boolean;
  last_seen: string | null;
  suspended: boolean;
  /** REL-51: 0 with no icon. */
  icon_version: number;
  /** REL-40: null when the bot is not in the showcase. */
  showcase: ShowcaseEntry | null;
}

export type NewToken = { id: number; username: string; name: string; token: string };

/**
 * CLI-24 and CLI-49: carries a new token from the create form to its bot's page, in memory only.
 * Only that bot's page can take it, and only once: a reload or another bot's page never shows it.
 */
export class TokenHandOver {
  private waiting: NewToken | null = null;

  give(token: NewToken): void {
    this.waiting = token;
  }

  /** The token of this bot, once; null for any other bot. A token for another bot is dropped. */
  take(botId: number): NewToken | null {
    const token = this.waiting;
    this.waiting = null;
    return token?.id === botId ? token : null;
  }
}

/** CLI-49: the bot of a page address, by username, without regard to case. */
export function botByUsername<T extends { username: string }>(bots: T[], username: string): T | null {
  const wanted = username.toLowerCase();
  return bots.find(bot => bot.username.toLowerCase() === wanted) ?? null;
}

export type SettingKind = "number" | "switch" | "text";

export interface Setting {
  key: string;
  value: string;
  start: string;
  changed: boolean;
  kind: SettingKind;
  restart: boolean;
  /** ADM-19: the description; a setting without one shows its key. */
  group?: string;
  name?: Partial<Record<"fa" | "en", string>>;
  help?: Partial<Record<"fa" | "en", string>>;
  unit?: string;
  min?: number | null;
  max?: number | null;
  risky?: boolean;
}

/** CLI-83: the readable name in the page language, else the key. */
export function settingName(setting: Setting, lang: "fa" | "en"): string {
  return setting.name?.[lang] || setting.name?.en || setting.key;
}

/** CLI-83: the search box keeps a row whose name (either language), key or help holds the words, without letter case. */
export function settingMatches(setting: Setting, words: string): boolean {
  const query = words.trim().toLowerCase();
  if (!query) return true;
  return [setting.key, setting.name?.fa, setting.name?.en, setting.help?.fa, setting.help?.en]
    .some(text => text?.toLowerCase().includes(query));
}

/** CLI-83: settings by service (ADM-19 `group`, else the key's first part), the services in a fixed order, then any other. */
export function settingGroups(settings: Setting[]): [string, Setting[]][] {
  const order = ["Identity", "Relay", "Notification", "Sites", "Admin", "Dalan"];
  const groups = new Map<string, Setting[]>();
  for (const setting of settings) {
    const group = setting.group || setting.key.split(":")[0]!;
    groups.set(group, [...(groups.get(group) ?? []), setting]);
  }
  const rank = (name: string) => order.includes(name) ? order.indexOf(name) : order.length;
  return [...groups.entries()].sort(([a], [b]) => rank(a) - rank(b) || a.localeCompare(b));
}

/** CLI-25: settings grouped by their first part ("Relay:MaxTextLength" is in "Relay"), groups in name order. */
export function groupSettings(settings: Setting[]): [string, Setting[]][] {
  const groups = new Map<string, Setting[]>();
  for (const setting of settings) {
    const group = setting.key.split(":")[0]!;
    groups.set(group, [...(groups.get(group) ?? []), setting]);
  }
  return [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));
}

/** CLI-25: the value as the server stores it, or null when it does not fit the kind (same rule as ADM-07). */
export function cleanValue(kind: SettingKind, text: string): string | null {
  const value = text.trim();
  switch (kind) {
    case "number":
      return /^[0-9]+$/.test(value) && Number(value) >= 1 && Number(value) <= 2147483647 ? String(Number(value)) : null;
    case "switch":
      return value === "true" || value === "false" ? value : null;
    default:
      return text;
  }
}

/** CLI-25: an access log time in UTC, for a letter to the police, like "2026-11-17 12:00:05 UTC". */
export function utc(iso: string): string {
  return new Date(iso).toISOString().replace("T", " ").replace(/\.\d+Z$/, " UTC");
}
