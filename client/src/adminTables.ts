// CLI-93: the cells of the admin tables and the address of a list page (ADM-11, ADM-12). Pure, so tests run without a browser.
import type { AdminBot, AdminUser } from "./api.ts";
import type { TextKey } from "./i18n.ts";

/** The query of a list page: the search when there is one, and `before` for the next page. Empty: everyone, newest first. */
export function adminListQuery(query: string, before: number | undefined): string {
  const text = query.trim();
  return new URLSearchParams([...(text ? [["q", text]] : []), ...(before !== undefined ? [["before", String(before)]] : [])]).toString();
}

const never = "—";

/** Name and username, phone or email, builder state, bots, joined, last active. */
export function userCells(user: AdminUser, t: (key: TextKey) => string, day: (iso: string) => string): string[] {
  return [
    user.username ? `${user.name} @${user.username}` : user.name,
    user.phone ?? user.email ?? "",
    t(user.builder === "approved" ? "approved" : user.builder === "requested" ? "requested" : user.builder === "refused" ? "refused" : "none"),
    String(user.bots ?? 0),
    day(user.created_at),
    user.last_active_at ? day(user.last_active_at) : never,
  ];
}

/** Name and username, owner, chat users, last seen. */
export function botCells(bot: AdminBot, day: (iso: string) => string): string[] {
  return [`${bot.name} @${bot.username}`, `#${bot.owner_user_id}`, String(bot.users ?? 0), bot.last_seen_at ? day(bot.last_seen_at) : never];
}
