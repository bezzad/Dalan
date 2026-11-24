// Rule: CLI-36
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { loginErrorText } from "../src/app.ts";
import { texts } from "../src/i18n.ts";
import { clock, isReturning, loginOrder, markReturning } from "../src/login.ts";

const settings = { dev_login: false, email_login: true, github_login: true, password_min_length: 8, name_max_length: 64 };

test("CLI-36 a new browser gets SMS, GitHub, email", () => {
  assert.deepEqual(loginOrder(settings, false), ["sms", "github", "email"]);
});

test("CLI-36 a returning browser gets GitHub, email, SMS", () => {
  assert.deepEqual(loginOrder(settings, true), ["github", "email", "sms"]);
});

test("CLI-36 a way that is off has no button", () => {
  assert.deepEqual(loginOrder({ ...settings, github_login: false }, true), ["email", "sms"]);
  assert.deepEqual(loginOrder({ ...settings, email_login: false, github_login: false }, false), ["sms"]);
});

test("CLI-36 the browser is returning after a login, and storage that fails is a new browser", () => {
  const saved = new Map<string, string>();
  const storage = { getItem: (key: string) => saved.get(key) ?? null, setItem: (key: string, value: string) => void saved.set(key, value) };
  assert.equal(isReturning(storage), false);
  markReturning(storage);
  assert.equal(saved.get("dalan.returning"), "1");
  assert.equal(isReturning(storage), true);

  const broken = { getItem: (): string | null => { throw new Error("blocked"); }, setItem: () => { throw new Error("blocked"); } };
  assert.equal(isReturning(broken), false);
  assert.doesNotThrow(() => markReturning(broken));
});

test("CLI-36 SMS and email are buttons like GitHub, with a way back", () => {
  const source = readFileSync(new URL("../src/login.ts", import.meta.url), "utf8");
  assert.match(source, /class: `button way \$\{way\}`/);
  assert.match(source, /class: "button way github"/);
  assert.match(source, /t\("back"\)/);
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

// Rule: CLI-61
test("CLI-61 the countdown reads m:ss, and h:mm:ss from an hour", () => {
  assert.equal(clock(120), "2:00");
  assert.equal(clock(1680), "28:00");
  assert.equal(clock(5), "0:05");
  assert.equal(clock(82800), "23:00:00");
});

test("CLI-61 the code screen can send again, and no text says tomorrow", () => {
  const source = readFileSync(new URL("../src/login.ts", import.meta.url), "utf8");
  assert.match(source, /t\("sendAgain"\)/);
  assert.match(source, /retry_in/);
  assert.match(source, /retryAfter/);
  for (const lang of [texts.en, texts.fa]) {
    for (const [key, text] of Object.entries(lang)) {
      if (/^(sms|email)/.test(key)) assert.doesNotMatch(text, /tomorrow|فردا/, key);
    }
  }
  assert.match(texts.en.tryAgainIn, /\{time\}/);
  assert.match(texts.fa.tryAgainIn, /\{time\}/);
});
