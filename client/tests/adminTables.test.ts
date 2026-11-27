// Rules: CLI-93, ADM-11, ADM-12, ADM-20
// The admin tables: everyone with no search, a page at a time; a row opens the user's or the bot's page.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { adminListQuery, botCells, userCells } from "../src/adminTables.ts";

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
