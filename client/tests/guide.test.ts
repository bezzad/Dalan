// Rules: CLI-38
import assert from "node:assert/strict";
import { test } from "node:test";
import { guideSteps, librarySamples } from "../src/guide.ts";
import { pageFor, type PublicNumbers } from "../src/pages.ts";

const numbers: PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://example.ir", bot_api_url: "https://api.example.ir" };
const persian = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
const english = (value: number) => String(value);

test("CLI-38 the guide has nine steps with the real addresses", () => {
  for (const [lang, n] of [["fa", persian], ["en", english]] as const) {
    const steps = guideSteps(lang, numbers, n);
    assert.equal(steps.length, 9);
    assert.equal(steps[4]!.code, "curl https://api.example.ir/bot<token>/getMe");
    assert.equal(steps[5]!.samples?.length, 7);
    assert.ok(steps[6]!.code!.startsWith("https://example.ir/@"));
    assert.ok(steps[8]!.text.includes(n(60)), `${lang} says the expiry`);
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

test("CLI-38 the guide opens for a visitor and a logged-in user", () => {
  assert.equal(pageFor("/guide", false), "guide");
  assert.equal(pageFor("/guide", true), "guide");
});
