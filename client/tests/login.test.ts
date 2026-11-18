// Rule: CLI-36
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { loginErrorText } from "../src/app.ts";
import { texts } from "../src/i18n.ts";
import { loginLayout } from "../src/login.ts";

const settings = { dev_login: false, email_login: true, github_login: true, password_min_length: 8, name_max_length: 64 };

test("CLI-36 GitHub comes first when it is on, then the email form", () => {
  assert.deepEqual(loginLayout(settings), { github: true, first: "email" });
});

test("CLI-36 no GitHub button when it is off; SMS when email is off", () => {
  assert.deepEqual(loginLayout({ ...settings, github_login: false, email_login: false }), { github: false, first: "sms" });
});

test("CLI-36 the GitHub button opens the server's start address", () => {
  const source = readFileSync(new URL("../src/login.ts", import.meta.url), "utf8");
  assert.match(source, /href: "\/api\/auth\/github"/);
  // The mark is drawn inline, never loaded from another host.
  assert.doesNotMatch(source, /https:\/\/(?!www\.w3\.org)/);
});

test("CLI-36 every GitHub login error has its own text in both languages", () => {
  for (const code of ["github_state", "github_denied", "github_no_email", "github_unavailable", "suspended"]) {
    const key = loginErrorText(`?login_error=${code}`);
    assert.ok(key && key !== "failed", code);
    assert.notEqual(texts.fa[key], texts.en[key]);
  }
});

test("CLI-36 the error text of an unreachable GitHub says to use email or SMS", () => {
  assert.equal(loginErrorText("?login_error=github_unavailable"), "githubUnavailable");
  assert.equal(texts.en.githubUnavailable, "GitHub cannot be reached now. Log in by email or SMS.");
});

test("CLI-36 no login_error in the address shows no error", () => {
  assert.equal(loginErrorText(""), null);
  assert.equal(loginErrorText("?x=1"), null);
  assert.equal(loginErrorText("?login_error=something_new"), "failed");
});
