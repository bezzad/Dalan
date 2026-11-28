// Rules: CLI-93, ADM-11, ADM-12, ADM-20, CLI-99
// The admin tables: everyone with no search, a page at a time; a row opens the user's or the bot's page.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { adminListQuery, backupDetail, backupSize, backupStatus, botCells, restoreAllowed, userCells } from "../src/adminTables.ts";
import { dashboardMenu, dashboardTarget, sectionPath, type SettingsUser } from "../src/settings/model.ts";

const t = (key: string) => ({ approved: "Approved", requested: "Requested", refused: "Refused", none: "None" } as Record<string, string>)[key] ?? key;
const day = (iso: string) => iso.slice(0, 10);
const source = (file: string) => readFileSync(new URL(`../src/${file}`, import.meta.url), "utf8");

test("ADM-11 and ADM-12 with no search the list asks for everyone; More asks before the last id", () => {
  assert.equal(adminListQuery("", undefined), "");
  assert.equal(adminListQuery("", 12), "before=12");
  assert.equal(adminListQuery(" sara ", 12), "q=sara&before=12");
  const api = source("api.ts");
  assert.match(api, /`\/api\/admin\/users\?\$\{adminListQuery\(query, before\)\}`/);
  assert.match(api, /`\/api\/admin\/bots\?\$\{adminListQuery\(query, before\)\}`/);
  assert.match(api, /"\/api\/admin\/users\?builder=requested"/);
});

test("CLI-93 a user's row: name and username, contact, builder, bots, joined, last active", () => {
  const sara = { id: 7, phone: "+989121234567", email: null, name: "Sara", username: "sara", created_at: "2026-11-01T10:00:00Z",
    builder: "approved" as const, builder_requested_at: null, suspended: false, bots: 2, last_active_at: "2026-11-20T10:00:00Z" };
  assert.deepEqual(userCells(sara, t, day), ["Sara @sara", "+989121234567", "Approved", "2", "2026-11-01", "2026-11-20"]);
  assert.deepEqual(userCells({ ...sara, phone: null, email: "s@x.ir", username: null, last_active_at: null }, t, day)[0], "Sara");
  assert.equal(userCells({ ...sara, phone: null, email: "s@x.ir", last_active_at: null }, t, day)[1], "s@x.ir");
  assert.equal(userCells({ ...sara, last_active_at: null }, t, day)[5], "—");
});

test("CLI-93 a bot's row: name and username, owner, chat users, last seen", () => {
  const bot = { id: 5, username: "mofidbot", name: "Mofid", owner_user_id: 7, created_at: "2026-11-01T10:00:00Z",
    last_seen_at: null, suspended: false, users: 3 };
  assert.deepEqual(botCells(bot, day), ["Mofid @mofidbot", "#7", "3", "—"]);
});

test("ADM-20 and CLI-93 a row opens its page; the page loads the user with bots, or the bot with its owner", () => {
  const admin = source("admin.ts");
  assert.match(admin, /h\("table"/);
  assert.match(admin, /adminDetailPath\("users", /);
  assert.match(admin, /adminDetailPath\("adminBots", /);
  assert.match(admin, /api\.adminUser\(/);
  assert.match(admin, /api\.adminBot\(/);
  const api = source("api.ts");
  assert.match(api, /`\/api\/admin\/users\/\$\{userId\}`/);
  assert.match(api, /`\/api\/admin\/bots\/\$\{botId\}`/);
  assert.match(source("settings/index.ts"), /adminPage\(app, section as AdminSection, target\.sub\)/);
});

const everyone: SettingsUser = { builder: "none", admin: false, has_phone: true, has_password: true, has_github: false, email: null };

test("CLI-99 the admin group has Backup at /dashboard/admin/backup; for anyone else it is Not found", () => {
  const group = dashboardMenu({ ...everyone, admin: true }).find(entry => entry.group === "admin")!;
  assert.ok(group.sections.some(section => section.id === "backup"));
  assert.equal(sectionPath("backup"), "/dashboard/admin/backup");
  assert.deepEqual(dashboardTarget("/dashboard/admin/backup", "", { ...everyone, admin: true }, true), { section: "backup", path: "/dashboard/admin/backup" });
  assert.equal(dashboardTarget("/dashboard/admin/backup", "", everyone, true).notFound, true);
});

test("CLI-99 a backup row: time, size and uploaded; the status lines of the scripts", () => {
  const backup = { name: "dalan-upload-2026-11-28-101500.tar.gz", size: 3 * 1024 * 1024, at: "2026-11-28T10:15:00Z", uploaded: true };
  const words = (key: string) => ({ backupUploaded: "uploaded", backupOk: "Done", backupFailed: "Failed" } as Record<string, string>)[key] ?? key;
  assert.equal(backupDetail(backup, words, day), "2026-11-28 · 3.0 MB · uploaded");
  assert.equal(backupDetail({ ...backup, uploaded: false, size: 10 }, words, day), "2026-11-28 · 1 KB");
  assert.equal(backupSize(2048), "2 KB");
  assert.deepEqual(backupStatus("ok 2026-11-28T07:00:00Z dalan-2026-11-28-070000.tar.gz", words, day),
    { ok: true, text: "Done · 2026-11-28", detail: "dalan-2026-11-28-070000.tar.gz" });
  assert.deepEqual(backupStatus("failed 2026-11-28T07:00:00Z pg_dump failed", words, day), { ok: false, text: "Failed · 2026-11-28", detail: "pg_dump failed" });
  assert.equal(backupStatus(null, words, day), null);
  assert.equal(backupStatus("something else", words, day), null);
});

test("CLI-99 Restore needs the word RESTORE and asks first; it says everyone logs in again", () => {
  assert.equal(restoreAllowed("RESTORE"), true);
  assert.equal(restoreAllowed(" RESTORE "), true);
  assert.equal(restoreAllowed("restore"), false);
  assert.equal(restoreAllowed(""), false);
  const admin = source("admin.ts");
  assert.match(admin, /!restoreAllowed\(word\.value\) \|\| !confirm\(t\("restoreConfirm"\)\)/);
  assert.match(admin, /class: "card danger column"/);
  const i18n = source("i18n.ts");
  assert.match(i18n, /restoreNote: ".*خود شما هم.*دوباره وارد شوند/);
  assert.match(i18n, /restoreNote: ".*everyone, you too, logs in again/);
  const api = source("api.ts");
  assert.match(api, /"\/api\/admin\/backups\/upload"/);
  assert.match(api, /`\/api\/admin\/backups\/\$\{encodeURIComponent\(name\)\}\/restore`, \{ confirm \}/);
});
