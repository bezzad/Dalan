// Rules: CLI-38
import assert from "node:assert/strict";
import { test } from "node:test";
import { botRunSteps, guideSteps, librarySamples, siteGuideSteps } from "../src/guide.ts";
import { pageFor, type PublicNumbers } from "../src/pages.ts";

const numbers: PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://example.ir", bot_api_url: "https://api.example.ir" };
const persian = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
const english = (value: number) => String(value);

test("CLI-38 the guide has nine steps with the real addresses", () => {
  for (const [lang, n] of [["fa", persian], ["en", english]] as const) {
    const steps = guideSteps(lang, numbers, n);
    assert.equal(steps.length, 9);
    assert.equal(steps[4]!.code, "curl https://api.example.ir/bot12:AbCdEfGhIjKlMnOp/getMe");
    assert.match(steps[4]!.text, /bot/, `${lang} says where the token goes`);
    assert.equal(steps[5]!.samples?.length, 7);
    assert.ok(steps[6]!.code!.startsWith("https://example.ir/@"));
    assert.ok(steps[8]!.text.includes(n(60)), `${lang} says the expiry`);
  }
});

test("CLI-38 My bots numbers its run steps from 1 and fills in the new token", () => {
  for (const [lang, n] of [["fa", persian], ["en", english]] as const) {
    const withToken = botRunSteps(lang, numbers, n, "7:XyZ");
    assert.deepEqual(withToken.map(step => step.title), guideSteps(lang, numbers, n).slice(4).map(step => step.title));
    assert.equal(withToken[0]!.code, "curl https://api.example.ir/bot7:XyZ/getMe");
    assert.doesNotMatch(withToken[0]!.text, /12:AbCd/, `${lang} does not talk about the sample`);
    assert.equal(botRunSteps(lang, numbers, n, null)[0]!.code, "curl https://api.example.ir/bot12:AbCdEfGhIjKlMnOp/getMe");
  }
});

test("CLI-38 every library sample points at Dalan, never at Telegram", () => {
  const samples = librarySamples("https://api.example.ir");
  assert.deepEqual(samples.map(sample => sample.name),
    ["python-telegram-bot", "aiogram", "pyTelegramBotAPI", "Telegraf", "grammY", "node-telegram-bot-api", "Telegram.Bot (.NET)"]);
  for (const sample of samples) {
    assert.ok(sample.code.includes("https://api.example.ir"), sample.name);
    assert.doesNotMatch(sample.code, /api\.telegram\.org/);
  }
});

test("CLI-38 the site guide has four steps: builder, one-line install, login, run", () => {
  for (const lang of ["fa", "en"] as const) {
    const steps = siteGuideSteps(lang, numbers);
    assert.equal(steps.length, 4);
    const install = steps[1]!.samples!.map(sample => sample.code);
    assert.deepEqual(install, ["curl -fsSL https://example.ir/install.sh | sh", "irm https://example.ir/install.ps1 | iex"]);
    assert.equal(steps[3]!.code, "dalan http 3000 --name mysite");
    assert.doesNotMatch(JSON.stringify(steps), /tunnel|تونل/i);
  }
});

test("CLI-38 the guide asks for a plan first, and opens for a visitor and a logged-in user", () => {
  for (const loggedIn of [false, true]) {
    assert.equal(pageFor("/guide", loggedIn), "guide");
    assert.equal(pageFor("/guide/bot", loggedIn), "guideBot");
    assert.equal(pageFor("/guide/site", loggedIn), "guideSite");
    assert.equal(pageFor("/guide/other", loggedIn), "notFound");
  }
});
