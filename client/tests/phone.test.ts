// Rule: CLI-60
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { phoneNumber } from "../src/digits.ts";

test("CLI-60 every usual way to write an Iranian mobile becomes 09…", () => {
  for (const written of [
    "+98 914 914 9202", "+989149149202", "0098-914-914-9202", "98 914 9149202", "(0914) 914 9202",
    "9149149202", "0914 914 9202", "۰۹۱۴ ۹۱۴ ۹۲۰۲", "+۹۸ ۹۱۴ ۹۱۴ ۹۲۰۲", "‎+98 914 914 9202‏",
  ]) {
    assert.equal(phoneNumber(written), "09149149202", written);
  }
});

test("CLI-60 text that is not an Iranian mobile only gets Latin digits", () => {
  assert.equal(phoneNumber(" sara@example.com "), "sara@example.com");
  assert.equal(phoneNumber("+1 555 123 4567"), "+1 555 123 4567");
  assert.equal(phoneNumber("۰۹۱۲"), "0912");
});

test("CLI-60 the login and account screens send the phone through phoneNumber", () => {
  const login = readFileSync(new URL("../src/login.ts", import.meta.url), "utf8");
  const account = readFileSync(new URL("../src/settings/profile.ts", import.meta.url), "utf8");
  assert.match(login, /clean: phoneNumber/);
  assert.match(login, /passwordLogin\(phoneNumber\(/);
  assert.match(login, /devLogin\(phoneNumber\(/);
  assert.match(account, /const number = phoneNumber\(/);
});
