// Pure logic of the account, builder and admin screens (CLI-23 to CLI-25), kept apart so tests run without a browser.

import type { ShowcaseEntry } from "./pages.ts";

export type BuilderState = "none" | "requested" | "approved";

/** CLI-23: what the account screen shows about building bots. */
export function builderView(user: { builder: BuilderState; admin: boolean }): "request" | "waiting" | "bots" {
  if (user.admin || user.builder === "approved") return "bots";
  return user.builder === "requested" ? "waiting" : "request";
}

export interface BuilderBot {
  id: number;
  username: string;
  name: string;
  created_at: string;
  last_seen: string | null;
  suspended: boolean;
  /** REL-40: null when the bot is not in the showcase. */
  showcase: ShowcaseEntry | null;
}

export type NewToken = { id: number; username: string; name: string; token: string };

/** CLI-24: the builder's list. A token is kept only until the builder closes it, and never in the list. */
export class BuilderList {
  bots: BuilderBot[] = [];
  shownToken: NewToken | null = null;

  /** A bot was created: it joins the list without its token, and the token is shown. */
  created(bot: NewToken, now: Date): void {
    this.bots = [...this.bots, { id: bot.id, username: bot.username, name: bot.name, created_at: now.toISOString(), last_seen: null, suspended: false, showcase: null }];
    this.shownToken = bot;
  }

  rotated(bot: NewToken): void {
    this.shownToken = bot;
  }

  closeToken(): void {
    this.shownToken = null;
  }
}

export type SettingKind = "number" | "switch" | "text";

export interface Setting {
  key: string;
  value: string;
  start: string;
  changed: boolean;
  kind: SettingKind;
  restart: boolean;
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
