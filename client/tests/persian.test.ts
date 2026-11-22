// Rule: CLI-43
// Persian is a little formal and uses the common word, never a literal translation of the English term.
import assert from "node:assert/strict";
import { test } from "node:test";
import { guideSteps, siteGuideSteps } from "../src/guide.ts";
import { texts } from "../src/i18n.ts";
import * as pages from "../src/pages.ts";

const numbers: pages.PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://dalanet.ir", bot_api_url: "https://api.dalanet.ir" };
const n = (value: number) => new Intl.NumberFormat("fa-IR").format(value);

/** Every string inside a value, with the path to it. */
function strings(value: unknown, path: string): [string, string][] {
  if (typeof value === "string") return [[path, value]];
  if (Array.isArray(value)) return value.flatMap((item, i) => strings(item, `${path}[${i}]`));
  if (value && typeof value === "object") return Object.entries(value).flatMap(([key, item]) => strings(item, `${path}.${key}`));
  return [];
}

const persian: [string, string][] = [
  ...strings(texts.fa, "texts.fa"),
  ...Object.entries(pages).flatMap(([name, value]) =>
    value && typeof value === "object" && "fa" in value ? strings((value as Record<string, unknown>)["fa"], `${name}.fa`) : []),
  ...strings(pages.privacyPoints("fa", numbers, n), "privacyPoints"),
  ...strings(pages.faqItems("fa", numbers, n), "faqItems"),
  ...strings(guideSteps("fa", numbers, n), "guideSteps"),
  ...strings(siteGuideSteps("fa", numbers), "siteGuideSteps"),
  ["readyPrompt", pages.readyPrompt("fa", numbers.site_url, numbers.bot_api_url)],
];

// A Persian letter, so that «ربات» or «دسکتاپ» never match «بات» or «اپ».
const letter = "[\\u0600-\\u06FF]";
const word = (text: string) => new RegExp(`(?<!${letter})${text}(?!${letter})`);
const literal: [string, RegExp][] = [
  ["بات", new RegExp(`(?<!${letter})بات`)], ["رله", /رله/], ["اپ", word("اپ")], ["ایجنت", /ایجنت/], ["ادمین", /ادمین/],
  ["پلن", /پلن/], ["لاگ", word("لاگ")], ["تم:", /(?<![؀-ۿ])تم:/], ["گواه کار", /گواه کار/],
  ["محدودیت سرعت", /محدودیت سرعت/], ["سوال‌های رایج", /سوال‌های رایج/], ["گفتگو", /گفتگو/],
];

test("CLI-43 Persian texts use the common words, not literal translations", () => {
  assert.ok(persian.length > 250, "the Persian texts were not found");
  const found = persian.flatMap(([path, text]) => literal.filter(([, pattern]) => pattern.test(text)).map(([name]) => `${path}: «${name}» in ${text}`));
  assert.deepEqual(found, []);
});

test("CLI-43 «می» is joined to its verb with a zero-width non-joiner", () => {
  const loose = persian.filter(([, text]) => /(?<![؀-ۿ])می /.test(text)).map(([path, text]) => `${path}: ${text}`);
  assert.deepEqual(loose, []);
});
