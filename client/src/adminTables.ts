// CLI-93: the cells of the admin tables and the address of a list page (ADM-11, ADM-12). Pure, so tests run without a browser.
import type { AdminBot, AdminUser, Backup } from "./api.ts";
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

/** CLI-99, ADM-22: the word the admin types before a restore. */
export const restoreWord = "RESTORE";

/** CLI-99: Restore opens only for the exact word (spaces around it do not count). */
export function restoreAllowed(typed: string): boolean {
  return typed.trim() === restoreWord;
}

/** A size in MB (one decimal) or KB. */
export function backupSize(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.ceil(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/** CLI-99: the grey line of a backup row: time, size, and "uploaded" for one the admin uploaded. */
export function backupDetail(backup: Backup, t: (key: TextKey) => string, time: (iso: string) => string): string {
  return [time(backup.at), backupSize(backup.size), ...(backup.uploaded ? [t("backupUploaded")] : [])].join(" · ");
}

/** CLI-99: a status file of the scripts ("ok|failed <time> <text>"): the words and the time; `detail` is the scripts' own
 * English text (an archive name or the reason). Null when there is none yet. */
export function backupStatus(status: string | null, t: (key: TextKey) => string, time: (iso: string) => string):
  { ok: boolean; text: string; detail: string } | null {
  const match = status ? /^(ok|failed) (\S+) ?(.*)$/.exec(status) : null;
  if (!match) return null;
  const ok = match[1] === "ok";
  return { ok, text: `${t(ok ? "backupOk" : "backupFailed")} · ${time(match[2]!)}`, detail: match[3] ?? "" };
}
