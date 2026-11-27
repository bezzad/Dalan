// Rules: CLI-81, CLI-82, CLI-83, CLI-92
// The dashboard: groups by role, addresses, the old addresses, "Not found" for admin pages; the admin overview and settings
// rows; the second factor in the app. The logic is pure; the screens are checked by their source.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { texts, type TextKey } from "../src/i18n.ts";
import { glyphs } from "../src/icon.ts";
import { pageFor } from "../src/pages.ts";
import { settingGroups, settingMatches, settingName, type Setting } from "../src/panels.ts";
import { dashboardAddress, dashboardMenu, dashboardTarget, sectionPath, sections, type SettingsUser } from "../src/settings/model.ts";
import { isTotpStep, recoveryFile, totpTicketIn } from "../src/totp.ts";
import { ApiError } from "../src/caller.ts";
import { errorText } from "../src/app.ts";
import { mergeRefusal } from "../src/merge.ts";

const source = (file: string) => readFileSync(new URL(file, import.meta.url), "utf8");
const user: SettingsUser = { builder: "none", admin: false, has_phone: true, has_password: true, has_github: false, email: null };
const builder: SettingsUser = { ...user, builder: "approved" };
const admin: SettingsUser = { ...user, admin: true };
const ids = (menu: ReturnType<typeof dashboardMenu>, group: string) => menu.find(entry => entry.group === group)?.sections.map(section => section.id);

test("CLI-81 a common user: Workspace with Chats only, and Account; no Admin group", () => {
  const menu = dashboardMenu(user);
  assert.deepEqual(menu.map(entry => entry.group), ["workspace", "account", "building"]);
  assert.deepEqual(ids(menu, "workspace"), ["chats"]);
  assert.equal(ids(menu, "admin"), undefined);
  // A builder also has My bots and My apps, still no Admin group.
  assert.deepEqual(ids(dashboardMenu(builder), "workspace"), ["chats", "bots", "apps"]);
  assert.equal(ids(dashboardMenu(builder), "admin"), undefined);
});

test("CLI-81 an admin also has the Admin group with its nine items", () => {
  assert.deepEqual(ids(dashboardMenu(admin), "admin"),
    ["overview", "requests", "users", "adminBots", "showcase", "messages", "adminSettings", "access", "audit"]);
  assert.deepEqual(sections.filter(section => section.group === "admin").map(section => sectionPath(section.id)), [
    "/dashboard/admin", "/dashboard/admin/requests", "/dashboard/admin/users", "/dashboard/admin/bots", "/dashboard/admin/showcase",
    "/dashboard/admin/messages", "/dashboard/admin/settings", "/dashboard/admin/access", "/dashboard/admin/audit"]);
  assert.deepEqual(dashboardTarget("/dashboard/admin/settings", "", admin, true), { section: "adminSettings", path: "/dashboard/admin/settings" });
});

test("CLI-81 a typed admin address for a common user is Not found, with no admin data", () => {
  for (const who of [user, builder]) {
    assert.deepEqual(dashboardTarget("/dashboard/admin/settings", "", who, true), { section: null, path: "/dashboard/admin/settings", notFound: true });
    assert.deepEqual(dashboardTarget("/dashboard/admin", "", who, false), { section: null, path: "/dashboard/admin", notFound: true });
  }
  assert.equal(dashboardTarget("/dashboard/nothing", "", admin, true).notFound, true);
  // The page draws "Not found" and never asks the admin API for it.
  const page = source("../src/settings/index.ts");
  assert.match(page, /target\.notFound\n\s*\/\/[^\n]*\n\s*\? h\("section", \{ class: "page" \}, h\("h2", \{ class: "head" \}, t\("notFoundTitle"\)\)/);
});

test("CLI-81 the old addresses move into the dashboard, with their query", () => {
  const moves: [string, string][] = [
    ["/admin", "/dashboard/admin"], ["/bots", "/dashboard/bots"], ["/chats", "/dashboard/chats"], ["/apps", "/dashboard/apps"],
    ["/bots/new", "/dashboard/bots/new"], ["/bots/mofidbot", "/dashboard/bots/mofidbot"],
    ["/account", "/dashboard"], ["/account/security", "/dashboard/security"], ["/account/keys", "/dashboard/keys"],
  ];
  for (const [from, to] of moves) assert.equal(dashboardAddress(from), to, from);
  for (const other of ["/", "/@mofidbot", "/privacy", "/dashboard/chats", "/guide/bot"]) assert.equal(dashboardAddress(other), null, other);
  const main = source("../src/main.ts");
  assert.match(main, /const moved = dashboardAddress\(location\.pathname\);\n\s*if \(moved\) history\.replaceState\(null, "", moved \+ location\.search\);/);
  // A visitor at an old or new address is asked to log in; after it, the address moves.
  for (const path of ["/admin", "/dashboard", "/dashboard/admin/users", "/account/profile"]) assert.equal(pageFor(path, false), "login", path);
  for (const path of ["/dashboard", "/dashboard/chats", "/dashboard/bots/new"]) assert.equal(pageFor(path, true), "dashboard", path);
});

test("CLI-81 My bots inside the frame, and a common user who opens it is sent to Builder (CLI-23)", () => {
  assert.deepEqual(dashboardTarget("/dashboard/bots/new", "", builder, true), { section: "bots", path: "/dashboard/bots/new", sub: "new" });
  assert.deepEqual(dashboardTarget("/dashboard/bots/mofidbot", "", builder, true), { section: "bots", path: "/dashboard/bots/mofidbot", sub: "mofidbot" });
  assert.deepEqual(dashboardTarget("/dashboard/bots", "", user, true), { section: "builder", path: "/dashboard/builder" });
  assert.deepEqual(dashboardTarget("/dashboard/apps", "", user, true), { section: "builder", path: "/dashboard/builder" });
  // A wide screen opens Chats; a phone the menu list.
  assert.deepEqual(dashboardTarget("/dashboard", "", user, true), { section: "chats", path: "/dashboard/chats" });
  assert.deepEqual(dashboardTarget("/dashboard", "", user, false), { section: null, path: "/dashboard" });
  // The chat with a bot stays full screen, outside the frame.
  assert.equal(pageFor("/@mofidbot", true), "chat");
});

test("CLI-81 the dashboard keeps a workspace screen's live updates", () => {
  const page = source("../src/settings/index.ts");
  assert.match(page, /return \{ \.\.\.inner, element \};/);
});

test("CLI-82 the overview cards link to their pages, requests count both kinds", () => {
  const admin = source("../src/admin.ts");
  assert.match(admin, /api\.adminOverview\(\)/);
  assert.match(admin, /card\(counts\.builder_requests \+ counts\.showcase_requests, "statRequests"/);
  for (const [field, section] of [["users", "users"], ["bots", "adminBots"], ["unread_messages", "messages"]] as const)
    assert.match(admin, new RegExp(`card\\(counts\\.${field}, "[a-zA-Z]+",[^\\n]*"${section}"\\)`), field);
});

const described: Setting = {
  key: "Admin:AccessLogRetentionDays", value: "400", start: "400", changed: false, kind: "number", restart: false,
  group: "Admin", name: { fa: "مدت نگهداری ثبت ورودها", en: "Access log kept for" }, help: { en: "How long the logins are kept." },
  unit: "days", min: 30, max: 3650, risky: true,
};

test("CLI-83 a setting shows its name in the page language; one without a description shows its key", () => {
  assert.equal(settingName(described, "en"), "Access log kept for");
  assert.equal(settingName(described, "fa"), "مدت نگهداری ثبت ورودها");
  const bare: Setting = { key: "Relay:Something", value: "1", start: "1", changed: false, kind: "number", restart: false };
  assert.equal(settingName(bare, "en"), "Relay:Something");
});

test("CLI-83 the search keeps rows whose name, key or help has the words", () => {
  for (const words of ["log", "LOG", "retention", "نگهداری", "kept", ""]) assert.ok(settingMatches(described, words), words);
  assert.ok(!settingMatches(described, "sms"));
});

test("CLI-83 settings are grouped by service in a fixed order", () => {
  const make = (key: string, group?: string): Setting => ({ key, value: "", start: "", changed: false, kind: "text", restart: false, ...(group ? { group } : {}) });
  const groups = settingGroups([make("Admin:A", "Admin"), make("Sites:B", "Sites"), make("Identity:C", "Identity"), make("Relay:D")]);
  assert.deepEqual(groups.map(([name]) => name), ["Identity", "Relay", "Sites", "Admin"]);
});

test("CLI-83 a row: range, Changed with Reset, Needs restart, a primary Save only when changed, a risky question", () => {
  const admin = source("../src/admin.ts");
  assert.match(admin, /save\.classList\.toggle\("primary", changed\)/);
  assert.match(admin, /if \(setting\.risky && !confirm\(`\$\{t\("riskyConfirm"\)\}\\n\$\{setting\.value \|\| "—"\} → \$\{value\}`\)\) return;/);
  assert.match(admin, /setting\.changed \? \[h\("span", \{ class: "badge off", title: setting\.start \}/);
  assert.match(admin, /setting\.restart \? \[h\("span", \{ class: "badge off" \}, t\("afterRestart"\)\)\]/);
  assert.match(admin, /api\.resetSetting\(setting\.key\)/);
});

test("CLI-92 a login that needs the code asks for it; GitHub comes back with ?totp=", () => {
  assert.equal(isTotpStep({ totp_required: true, ticket: "t" }), true);
  assert.equal(isTotpStep({ id: 1 } as never), false);
  assert.equal(totpTicketIn("?totp=abc"), "abc");
  assert.equal(totpTicketIn("?merge=1"), null);
  const login = source("../src/login.ts");
  assert.match(login, /if \(isTotpStep\(answer\)\) show\(totpStep\(answer\.ticket\)\);/);
  assert.match(login, /onLogin\(await api\.totpLogin\(ticket, latinDigits\(code\.value\.trim\(\)\)\)\)/);
  // Every login way goes through the step: SMS and email codes, password and the development login.
  for (const way of ["way.verify(", "api.passwordLogin(", "api.devLogin("]) assert.ok(login.includes(`loggedIn(await ${way}`), way);
  assert.match(source("../src/main.ts"), /if \(totpTicketIn\(location\.search\)\) return showLogin\(\);/);
});

test("CLI-92 the card: QR as SVG nodes (never HTML), recovery codes once with Copy and Download, no Turn off for an admin", () => {
  const card = source("../src/settings/totpCard.ts");
  assert.match(card, /new DOMParser\(\)\.parseFromString\(svg, "image\/svg\+xml"\)/);
  assert.doesNotMatch(card, /innerHTML/);
  assert.match(card, /on \? \(app\.user\.admin \? \[\] : \[off\]\) : \[start\]/);
  assert.match(card, /download: "dalan-recovery-codes\.txt"/);
  assert.equal(recoveryFile(["a1", "b2"], "Recovery codes"), "Recovery codes\n\na1\nb2\n");
  // In Login and security for a user with a password or an admin; the admin pages show only it until it is on (SEC-15).
  assert.match(source("../src/settings/security.ts"), /app\.user\.has_password \|\| app\.user\.admin \? \[secondFactor\] : \[\]/);
  assert.match(source("../src/settings/index.ts"), /app\.user\.totp \? adminPage\(app, section as AdminSection\) : h\("div", \{ class: "column" \},\n\s*h\("p", \{ class: "note" \}, t\("adminNeeds2fa"\)\), totpCard\(app\)\)/);
});

test("CLI-81 CLI-82 CLI-83 CLI-92 every new text in both languages, every menu icon drawn in the page", () => {
  const keys: TextKey[] = ["dashboard", "groupWorkspace", "groupAdmin", "adminNeeds2fa", "statUsers", "statBots", "statRequests", "statMessages",
    "statSites", "riskyConfirm", "settingSearch", "totpTitle", "totpHelp", "totpAdminHelp", "totpNote", "totpScan", "recoveryCodesTitle", "recoveryCodesNote",
    ...sections.flatMap(section => section.help ? [section.title, section.help] : [section.title])];
  for (const key of keys) {
    assert.ok(texts.fa[key] && texts.en[key], key);
    assert.notEqual(texts.fa[key], texts.en[key], key);
  }
  for (const section of sections) assert.ok(section.glyph in glyphs, section.glyph);
});

test("CLI-92 the new refusals have their own words: admin merge, admin without the code, TOTP on, out of range", () => {
  const codes: [string, TextKey][] = [["admin_merge", "adminMerge"], ["admin_needs_2fa", "adminNeeds2fa"], ["totp_on", "totpOn"],
    ["totp_not_allowed", "totpNotAllowed"], ["out_of_range", "outOfRange"], ["wrong_code", "wrongCode"]];
  for (const [code, key] of codes) {
    assert.equal(errorText(new ApiError(403, code)), key, code);
    assert.ok(texts.fa[key] && texts.en[key] && texts.fa[key] !== texts.en[key], key);
  }
  // SEC-15: a merge refused because one side is an admin says so, not "suspended".
  const t = (key: TextKey) => texts.en[key];
  const side = { name: "x", bots: 0, sites: 0, keys: 0, chats: 0 };
  assert.equal(mergeRefusal({ way: "phone", keep: "this", this: side, other: side, refusal: "admin" } as never, t), texts.en.adminMerge);
});
