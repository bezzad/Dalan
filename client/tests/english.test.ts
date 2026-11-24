// Rule: CLI-41
// English text never names Iran (sanctions, search engines). Persian text may say ایران.
import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import { test } from "node:test";
import { guideSteps, siteGuideSteps } from "../src/guide.ts";
import { texts } from "../src/i18n.ts";
import * as pages from "../src/pages.ts";

const iran = /iran/i;
const numbers: pages.PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://dalanet.ir", bot_api_url: "https://api.dalanet.ir", contact_max_length: 2000 };
const n = (value: number) => String(value);

/** Every string inside a value, with the path to it. */
function strings(value: unknown, path: string): [string, string][] {
  if (typeof value === "string") return [[path, value]];
  if (Array.isArray(value)) return value.flatMap((item, i) => strings(item, `${path}[${i}]`));
  if (value && typeof value === "object") return Object.entries(value).flatMap(([key, item]) => strings(item, `${path}.${key}`));
  return [];
}

test("CLI-41 no English text of the client names Iran", () => {
  const english: [string, string][] = [
    ...strings(texts.en, "texts.en"),
    // Every page text kept per language (landing, hero, products, offers, security, closing, labels, ...).
    ...Object.entries(pages).flatMap(([name, value]) =>
      value && typeof value === "object" && "en" in value ? strings((value as Record<string, unknown>)["en"], `${name}.en`) : []),
    ...strings(pages.privacyPoints("en", numbers, n), "privacyPoints"),
    ...strings(pages.faqItems("en", numbers, n), "faqItems"),
    ...strings(guideSteps("en", numbers, n), "guideSteps"),
    ...strings(siteGuideSteps("en", numbers), "siteGuideSteps"),
  ];
  assert.ok(english.length > 100, "the English texts were not found");
  const found = english.filter(([, text]) => iran.test(text)).map(([path, text]) => `${path}: ${text}`);
  assert.deepEqual(found, []);
});

test("CLI-41 the built client files never name Iran in Latin letters", () => {
  const dist = new URL("../dist/", import.meta.url);
  const files = readdirSync(dist, { recursive: true, encoding: "utf8" }).filter(file => /\.(html|js|json|webmanifest|svg|txt)$/.test(file));
  assert.ok(files.includes("index.html") && files.includes("main.js"), "the client is not built: run npm run build");
  for (const file of files) {
    const found = readFileSync(new URL(file, dist), "utf8").match(/.{0,40}iran.{0,40}/gi);
    assert.equal(found, null, `${file}: ${found?.join(" | ")}`);
  }
});
