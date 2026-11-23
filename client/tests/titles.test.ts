// Rule: CLI-44
// A title, heading, label or button text is not a sentence: it never ends with a full stop.
import assert from "node:assert/strict";
import { test } from "node:test";
import { guideSteps, siteGuideSteps } from "../src/guide.ts";
import { texts } from "../src/i18n.ts";
import * as pages from "../src/pages.ts";

const numbers: pages.PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://dalanet.ir", bot_api_url: "https://api.dalanet.ir" };
const n = (value: number) => String(value);

/** Every string under a key that names a title, with the path to it. */
function titles(value: unknown, path: string, keys: Set<string>): [string, string][] {
  if (Array.isArray(value)) return value.flatMap((item, i) => titles(item, `${path}[${i}]`, keys));
  if (!value || typeof value !== "object") return [];
  return Object.entries(value).flatMap(([key, item]) =>
    typeof item === "string" ? (keys.has(key) ? [[`${path}.${key}`, item] as [string, string]] : []) : titles(item, `${path}.${key}`, keys));
}

const titleKeys = new Set(["title", "glow", "label"]);
const textKeys = new Set(Object.keys(texts.en).filter(key => /Title$/.test(key) || key === "guideRun" || key === "guideChoose"));
const all: [string, string][] = (["fa", "en"] as const).flatMap(lang => [
  ...titles(pages.landing[lang], `landing.${lang}`, titleKeys),
  ...titles(pages.products[lang], `products.${lang}`, titleKeys),
  ...titles(pages.offers[lang], `offers.${lang}`, titleKeys),
  ...titles(pages.hero[lang], `hero.${lang}`, titleKeys),
  ...titles(pages.closing[lang], `closing.${lang}`, titleKeys),
  ...Object.entries(pages.labels[lang]).map(([key, text]) => [`labels.${lang}.${key}`, text] as [string, string]),
  ...titles(guideSteps(lang, numbers, n), `guideSteps.${lang}`, titleKeys),
  ...titles(siteGuideSteps(lang, numbers), `siteGuideSteps.${lang}`, titleKeys),
  ...[...textKeys].map(key => [`texts.${lang}.${key}`, texts[lang][key as keyof typeof texts.en]] as [string, string]),
]);

test("CLI-44 no title ends with a full stop", () => {
  assert.ok(all.length > 60, "the titles were not found");
  assert.deepEqual(all.filter(([, text]) => /[.۔]$/.test(text)).map(([path, text]) => `${path}: ${text}`), []);
});
