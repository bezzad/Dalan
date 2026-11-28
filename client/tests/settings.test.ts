// Rules: CLI-77, CLI-78
// CLI-81 moved the settings area into the dashboard (/dashboard/...); the rules of CLI-77 still hold there.
// The settings area (CLI-77) and the grouped user menu (CLI-78). The logic is pure; the screens are checked by their source.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { texts, type TextKey } from "../src/i18n.ts";
import { glyphs } from "../src/icon.ts";
import { pageFor } from "../src/pages.ts";
import {
  adminDetailPath, changedValue, dashboardMenu as settingsMenu, dashboardTarget as settingsTarget, loginMethods, menuSecondLine, sectionPath, sections, userMenuGroups,
  type SettingsUser,
} from "../src/settings/model.ts";

const source = (file: string) => readFileSync(new URL(file, import.meta.url), "utf8");
const user: SettingsUser = { builder: "none", admin: false, has_phone: true, has_password: true, has_github: false, email: null };
const builder: SettingsUser = { ...user, builder: "approved" };
const admin: SettingsUser = { ...user, admin: true };

test("CLI-77 desktop: the side menu has the Account group, the current item is Profile", () => {
  const menu = settingsMenu(user);
  assert.deepEqual(menu.map(entry => entry.group), ["workspace", "account", "building"]);
  assert.deepEqual(menu[1]!.sections.map(section => section.id), ["profile", "security", "notifications", "preferences"]);
  assert.deepEqual(settingsTarget("/dashboard/profile", "", user, true), { section: "profile", path: "/dashboard/profile" });
});

test("CLI-77 one address per section", () => {
  const settings = sections.filter(section => section.group === "account" || section.group === "building");
  assert.deepEqual(settings.map(section => sectionPath(section.id)),
    ["/dashboard/profile", "/dashboard/security", "/dashboard/notifications", "/dashboard/preferences", "/dashboard/builder", "/dashboard/keys"]);
  assert.deepEqual(settingsTarget("/dashboard/security", "", user, true), { section: "security", path: "/dashboard/security" });
  assert.deepEqual(settingsTarget("/dashboard/security", "", user, false), { section: "security", path: "/dashboard/security" });
  // Every address is a screen of a logged-in user, and the host serves it (CLI-01).
  for (const section of settings) assert.equal(pageFor(sectionPath(section.id), true), "dashboard", section.id);
  assert.equal(pageFor("/dashboard/profile", false), "login");
  assert.match(source("../../src/Dalan.Host/ClientApp.cs"), /"\/dashboard\/\{\*\*rest\}"/);
});

test("CLI-77 the Builder section is for every user (CLI-23); the access keys only for an approved builder or an admin", () => {
  for (const who of [builder, admin]) {
    assert.deepEqual(settingsMenu(who).find(entry => entry.group === "building")!.sections.map(section => section.id), ["builder", "keys"]);
    assert.deepEqual(settingsTarget("/dashboard/keys", "", who, true), { section: "keys", path: "/dashboard/keys" });
  }
  for (const waiting of ["none", "requested", "refused"] as const) {
    const who = { ...user, builder: waiting };
    assert.deepEqual(settingsMenu(who).find(entry => entry.group === "building")!.sections.map(section => section.id), ["builder"], waiting);
    assert.deepEqual(settingsTarget("/dashboard/keys", "", who, true), { section: "profile", path: "/dashboard/profile" });
    assert.deepEqual(settingsTarget("/dashboard/builder", "", who, true), { section: "builder", path: "/dashboard/builder" });
  }
  // Waiting for the phone: the Builder section says a phone is needed (CLI-34); no keys.
  assert.deepEqual(settingsMenu({ ...user, has_phone: false }).find(entry => entry.group === "building")!.sections.map(section => section.id), ["builder"]);
  // CLI-23: the section says a phone is needed, refused, or opens My bots; My apps sends a non-builder there.
  const section = source("../src/settings/building.ts");
  for (const key of ["builderCan", "phoneRequired", "builderRefused"]) assert.match(section, new RegExp(`t\\("${key}"\\)`), key);
  assert.match(source("../src/apps.ts"), /app\.go\("\/account\/builder"\)/);
  // CLI-81: that old address moves into the dashboard.
});

test("CLI-77 /dashboard on a phone is the menu, with the section's own address for each item", () => {
  assert.deepEqual(settingsTarget("/dashboard", "", user, false), { section: null, path: "/dashboard" });
  assert.deepEqual(settingsTarget("/dashboard/notifications", "", user, false), { section: "notifications", path: "/dashboard/notifications" });
  const css = source("../public/app.css");
  assert.match(css, /@media \(max-width: 759px\) \{[^@]*\.settings:not\(\.menu-only\) nav\.side/);
  assert.match(css, /nav\.side a \{ min-height: 48px; \}/);
  assert.match(css, /\.setting-row \{ grid-template-columns: 1fr; \}/);
  assert.match(source("../src/settings/index.ts"), /"‹ ", t\("dashboard"\)/);
});

test("CLI-77 the way back from GitHub, which names /account (now /dashboard), opens Login and security", () => {
  assert.deepEqual(settingsTarget("/dashboard", "?merge=1", user, true), { section: "security", path: "/dashboard/security" });
  assert.deepEqual(settingsTarget("/dashboard", "?link_error=has_github", user, false), { section: "security", path: "/dashboard/security" });
  const security = source("../src/settings/security.ts");
  assert.match(security, /query\.get\("merge"\) === "1"/);
  assert.match(security, /query\.get\("link_error"\) === "has_github"/);
});

test("CLI-77 login methods: Password and Phone connected, Email and GitHub not", () => {
  const methods = loginMethods(user, true);
  assert.deepEqual(methods.map(method => [method.id, method.connected, method.action]),
    [["password", true, "change"], ["phone", true, null], ["email", false, "setUp"], ["github", false, "link"]]);
  assert.equal(loginMethods({ ...user, has_password: false }, true)[0]!.action, "setUp");
  assert.equal(loginMethods({ ...user, email: "sara@example.com" }, true)[2]!.detail, "sara@example.com");
  // GitHub is left out when the server has it off and it is not linked.
  assert.deepEqual(loginMethods(user, false).map(method => method.id), ["password", "phone", "email"]);
  assert.equal(loginMethods({ ...user, has_github: true }, false)[3]!.connected, true);
  const security = source("../src/settings/security.ts");
  assert.match(security, /href: "\/api\/account\/github"/);
  assert.match(source("../src/settings/row.ts"), /t\(on \? "connected" : "notConnected"\)/);
});

test("CLI-77 a row's button is plain, and primary only while its value changed", () => {
  assert.equal(changedValue("sara", "sara"), false);
  assert.equal(changedValue("sara", " sara "), false);
  assert.equal(changedValue("sara", "sara2"), true);
  assert.equal(changedValue("", "0912"), true);
  const row = source("../src/settings/row.ts");
  assert.match(row, /h\("button", \{ type: "submit" \}, options\.label\)/);
  assert.match(row, /button\.classList\.toggle\("primary", options\.inputs\.some\(/);
  assert.match(row, /changedValue\(saved\[i\] \?\? "", input\.value\)/);
  // No section draws a primary button of its own.
  for (const file of ["profile", "security", "notifications", "preferences", "building"])
    assert.doesNotMatch(source(`../src/settings/${file}.ts`), /class: "primary"/, file);
});

test("CLI-77 the profile rows: username, phone and email, with the verify flows of CLI-34 and CLI-70", () => {
  const profile = source("../src/settings/profile.ts");
  assert.match(profile, /api\.setUsername\(/);
  assert.match(profile, /const number = phoneNumber\(/);
  assert.match(profile, /api\.addPhone\(/);
  assert.match(profile, /api\.addEmail\(/);
  assert.match(profile, /isMergeNeeded\(failure\)/);
  assert.match(profile, /\?merge=1/);
});

test("CLI-77 the merge card stays, with its Merge button only when the merge can be done", () => {
  const card = source("../src/settings/mergeCard.ts");
  assert.match(card, /\.\.\.\(refusal \? \[\] : \[mergeButton\]\)/);
  assert.match(card, /api\.dropMerge\(\)/);
  assert.match(card, /failure\.code === "merge_needed"/);
  assert.match(source("../src/settings/security.ts"), /mergeCard\(app\)/);
});

test("CLI-77 theme and language are radio cards that apply at once", () => {
  const preferences = source("../src/settings/preferences.ts");
  assert.match(preferences, /type: "radio", name: group/);
  assert.match(preferences, /app\.theme\.choose\(name\)/);
  assert.match(preferences, /app\.language\.toggle\(\)/);
  assert.match(preferences, /app\.refresh\(\)/);
  assert.match(preferences, /\["fa", "فارسی"\], \["en", "English"\]/);
});

test("CLI-77 the Builder section points to My bots and My apps; Access keys keeps the keys", () => {
  const building = source("../src/settings/building.ts");
  assert.match(building, /open\(t\("myBots"\), "\/bots"\)/);
  assert.match(building, /open\(t\("myApps"\), "\/apps"\)/);
  assert.match(building, /agentsPart\(app\)/);
  // "My bots" and "My apps" link to the section (m25 moved the keys into the account, CLI-30).
  assert.match(source("../src/builder.ts"), /href: "\/account\/keys"/);
});

test("CLI-77 each section has a title and a one-line help in both languages", () => {
  const keys: TextKey[] = ["settings", "groupAccount", "groupBuilding", "connected", "notConnected", "setUp", "change", "link",
    "themeLabel", "languageLabel", "themeLightName", "themeDarkName", ...sections.flatMap(section => section.help ? [section.title, section.help] : [section.title])];
  for (const key of keys) assert.notEqual(texts.fa[key], texts.en[key], key);
  assert.equal(texts.en.sectionSecurity, "Login and security");
  assert.equal(texts.en.connected, "Connected");
  assert.equal(texts.en.notConnected, "Not connected");
});

test("CLI-77 every icon of the area is drawn in the page", () => {
  for (const name of ["profile", "lock", "bell", "sliders", "wrench", "key", "bots", "chats", "apps", "settings", "shield", "logout"] as const)
    assert.ok(glyphs[name].length > 0, name);
  for (const section of sections) assert.ok(section.glyph in glyphs, section.id);
});

test("CLI-77 the design system describes the settings area and its pieces", () => {
  const ui = source("../../docs/UI.md");
  for (const piece of ["## Settings", "Setting row", "Status badge", "Radio card", "Danger card", "Empty state", "Grouped menu", "Breadcrumb", "Side menu"])
    assert.ok(ui.includes(piece), piece);
  const css = source("../public/app.css");
  for (const rule of [".setting-row", ".badge.ok", ".badge.off", ".method.card", ".radio-card", ".card.danger", ".empty", "nav.side", ".crumbs"])
    assert.ok(css.includes(rule), rule);
});

test("CLI-78 the user menu for an admin: three groups, an icon per item; Dashboard opens the admin overview", () => {
  const groups = userMenuGroups({ admin: true });
  assert.deepEqual(groups.map(group => group.map(item => item.id)), [["myBots", "chats", "myApps"], ["dashboard"], ["logout"]]);
  for (const item of groups.flat()) assert.ok(item.glyph in glyphs, item.id);
  // CLI-81: each item opens its place in the dashboard.
  assert.deepEqual(groups.flat().map(item => item.path), ["/dashboard/bots", "/dashboard/chats", "/dashboard/apps", "/dashboard/admin", null]);
});

test("CLI-78 a user who is not an admin has only Dashboard in the second group, opening /dashboard", () => {
  const second = userMenuGroups({ admin: false })[1]!;
  assert.deepEqual(second.map(item => [item.id, item.path]), [["dashboard", "/dashboard"]]);
});

test("CLI-93 a user's and a bot's page have their own address, only for an admin", () => {
  assert.deepEqual(settingsTarget("/dashboard/admin/users/7", "", admin, true), { section: "users", path: "/dashboard/admin/users/7", sub: "7" });
  assert.deepEqual(settingsTarget("/dashboard/admin/bots/5", "", admin, false), { section: "adminBots", path: "/dashboard/admin/bots/5", sub: "5" });
  assert.equal(settingsTarget("/dashboard/admin/users/7", "", user, true).notFound, true);
  assert.equal(settingsTarget("/dashboard/admin/users/x7", "", admin, true).notFound, true);
  assert.equal(adminDetailPath("users", 7), "/dashboard/admin/users/7");
  assert.equal(adminDetailPath("adminBots", 5), "/dashboard/admin/bots/5");
});

test("CLI-78 the menu opens with the name and, in grey, the email", () => {
  assert.equal(menuSecondLine({ email: "a@example.com" }), "a@example.com");
  assert.equal(menuSecondLine({ email: null }), null);
  const main = source("../src/main.ts");
  assert.match(main, /h\("strong", \{ dir: "auto" \}, current\.user\.name\)/);
  assert.match(main, /userMenuGroups\(current\.user\)/);
  assert.match(main, /h\("hr", \{\}\)/);
  const css = source("../public/app.css");
  assert.match(css, /\.menu\.user \.panel hr \{/);
  assert.match(css, /\.menu\.user \.panel \.item \.glyph \{ width: 18px; height: 18px; \}/);
});
