// Rule: CLI-59
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { readSmsCode } from "../src/webOtp.ts";

/** A browser's credentials store that keeps each request and answers as the test says. */
function credentials(answer: () => Promise<unknown>) {
  const asked: CredentialRequestOptions[] = [];
  return { asked, store: { get: (options: CredentialRequestOptions) => { asked.push(options); return answer(); } } };
}

test("CLI-59 with WebOTP the code from the SMS comes back", async () => {
  const { asked, store } = credentials(async () => ({ type: "otp", code: "123456" }));
  const signal = new AbortController().signal;

  assert.equal(await readSmsCode(signal, { webOtp: true, credentials: store }), "123456");
  assert.deepEqual(asked, [{ otp: { transport: ["sms"] }, signal }]);
});

test("CLI-59 without WebOTP no request is made", async () => {
  const { asked, store } = credentials(async () => ({ code: "123456" }));

  assert.equal(await readSmsCode(new AbortController().signal, { webOtp: false, credentials: store }), null);
  assert.equal(asked.length, 0);
});

test("CLI-59 a refused or aborted request gives nothing and no error", async () => {
  const refused = credentials(() => Promise.reject(new DOMException("No", "AbortError")));
  const empty = credentials(async () => null);

  assert.equal(await readSmsCode(new AbortController().signal, { webOtp: true, credentials: refused.store }), null);
  assert.equal(await readSmsCode(new AbortController().signal, { webOtp: true, credentials: empty.store }), null);
});

test("CLI-59 the SMS code screen keeps one-time-code, asks for the code and ends the request", () => {
  const source = readFileSync(new URL("../src/login.ts", import.meta.url), "utf8");
  assert.match(source, /autocomplete: "one-time-code"/);
  assert.match(source, /readSmsCode\(/);
  assert.match(source, /\.abort\(\)/);
});
