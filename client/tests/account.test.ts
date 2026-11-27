// Rules: AUTH-35, AUTH-36, AUTH-38
// The account screens: the sessions list, delete my account and the address limits of a key.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { errorText } from "../src/app.ts";
import { ApiError } from "../src/caller.ts";
import { deleteProof, deleteWord, parseAddresses, sessionName, sortedSessions } from "../src/settings/account.ts";
import { texts } from "../src/i18n.ts";

const source = (file: string) => readFileSync(new URL(`../src/${file}`, import.meta.url), "utf8");

test("AUTH-35 the list puts this device first, then the newest use; each has a name", () => {
  const sessions = [
    { id: 1, browser: "Firefox", system: "Windows", address: "1.2.3.4", created_at: "2026-11-01T00:00:00Z", last_used_at: "2026-11-02T00:00:00Z", current: false },
    { id: 2, browser: "Safari", system: "iOS", address: "5.6.7.8", created_at: "2026-11-01T00:00:00Z", last_used_at: "2026-11-20T00:00:00Z", current: false },
    { id: 3, browser: "", system: "", address: null, created_at: "2026-11-01T00:00:00Z", last_used_at: "2026-11-01T00:00:00Z", current: true },
  ];
  assert.deepEqual(sortedSessions(sessions).map(s => s.id), [3, 2, 1]);
  assert.equal(sessionName(sessions[0]!, "?"), "Firefox · Windows");
  assert.equal(sessionName(sessions[2]!, "Unknown device"), "Unknown device");
});

test("AUTH-35 Login and security lists the sessions, ends one or all others", () => {
  const security = source("settings/security.ts");
  assert.match(security, /sessionsCard\(app\)/);
  const card = source("settings/account.ts");
  assert.match(card, /api\.sessions\(\)/);
  assert.match(card, /api\.endSession\(/);
  assert.match(card, /api\.endOtherSessions\(\)/);
  const api = source("api.ts");
  assert.match(api, /"GET", "\/api\/account\/sessions"/);
  assert.match(api, /"DELETE", `\/api\/account\/sessions\/\$\{encodeURIComponent\(String\(id\)\)\}`/);
  assert.match(api, /"DELETE", "\/api\/account\/sessions"\)/);
});

test("AUTH-36 the proof is the password, or a code for a user with none; an admin has no delete card", () => {
  assert.equal(deleteProof({ has_password: true }), "password");
  assert.equal(deleteProof({ has_password: false }), "code");
  const profile = source("settings/profile.ts");
  assert.match(profile, /app\.user\.admin \? \[\] : \[deleteCard\(app\)\]/);
  const api = source("api.ts");
  assert.match(api, /"POST", "\/api\/account\/delete\/challenge"/);
  assert.match(api, /"POST", "\/api\/account\/delete\/code", \{ challenge, nonce \}/);
  assert.match(api, /"POST", "\/api\/account\/delete", proof/);
});

test("AUTH-36 and AUTH-38 every new error has its own text in both languages", () => {
  const codes = ["owns_things", "bad_word", "bad_proof", "admin_delete", "bad_address", "too_many_addresses", "key_address_refused"];
  const keys = codes.map(code => errorText(new ApiError(400, code)));
  assert.equal(new Set(keys).size, codes.length, keys.join());
  for (const key of keys) {
    assert.notEqual(key, "failed");
    assert.ok(texts.fa[key] && texts.en[key], key);
  }
});

test("AUTH-38 addresses are typed with spaces, commas or lines, Persian digits too; repeats go", () => {
  assert.deepEqual(parseAddresses(" 80.244.7.0/24, 2a01::1\n80.244.7.0/24 ، ۵.۶.۷.۸ "), ["80.244.7.0/24", "2a01::1", "5.6.7.8"]);
  assert.deepEqual(parseAddresses("  "), []);
  const api = source("api.ts");
  assert.match(api, /"PUT", `\/api\/account\/keys\/\$\{keyId\}\/addresses`, \{ addresses \}/);
  assert.match(source("agents.ts"), /api\.setKeyAddresses\(/);
});

test("AUTH-36 the card names the server's confirm word, in the user's script", () => {
  assert.equal(deleteWord(["DELETE", "حذف"], "fa"), "حذف");
  assert.equal(deleteWord(["DELETE", "حذف"], "en"), "DELETE");
  assert.equal(deleteWord(["REMOVE"], "fa"), "REMOVE", "only a Latin word: it is shown in Persian too");
  assert.equal(deleteWord(undefined, "en"), "DELETE");
  assert.match(texts.fa.deleteAccountNote, /\{w\}/);
  assert.match(texts.en.deleteAccountNote, /\{w\}/);
});
