// Rules: CLI-79
// The calendar choice: Jalali or Gregorian for every date the client shows, kept on the device.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { Language } from "../src/i18n.ts";

function language(lang: "fa" | "en", calendar?: string) {
  const store = new Map<string, string>([["dalan.lang", lang], ...(calendar ? [["dalan.calendar", calendar] as [string, string]] : [])]);
  const storage = { getItem: (key: string) => store.get(key) ?? null, setItem: (key: string, value: string) => void store.set(key, value) };
  return { language: new Language({ lang: "", dir: "" }, storage), store };
}

const expiry = "2026-11-27T12:00:00Z";

test("CLI-79 with no choice, Persian shows a Jalali date in Persian digits and English a Gregorian one", () => {
  assert.equal(language("fa").language.calendar, "jalali");
  assert.match(language("fa").language.day(expiry), /^۶ آذر ۱۴۰۵$/);
  assert.equal(language("en").language.calendar, "gregorian");
  assert.equal(language("en").language.day(expiry), "27 Nov 2026");
});

test("CLI-79 Gregorian in Persian: Persian month names and digits", () => {
  assert.equal(language("fa", "gregorian").language.day(expiry), "۲۷ نوامبر ۲۰۲۶");
  assert.match(language("en", "jalali").language.day(expiry), /Azar 1405/);
});

test("CLI-79 the choice applies at once and is kept on this device", () => {
  const { language: fa, store } = language("fa");
  fa.chooseCalendar("gregorian");
  assert.equal(store.get("dalan.calendar"), "gregorian");
  assert.equal(fa.day(expiry), "۲۷ نوامبر ۲۰۲۶");
  assert.match(fa.seen({ online: false, last_seen: expiry }, new Date("2026-12-10T00:00:00Z")), /نوامبر/);
  assert.equal(language("fa", "nonsense").language.calendar, "jalali", "a broken value is no choice");
});

test("CLI-79 every date in the client goes through the language, never its own locale", () => {
  for (const file of ["admin.ts", "agents.ts", "builder.ts", "chat.ts", "settings/security.ts", "settings/building.ts"]) {
    const source = readFileSync(new URL(`../src/${file}`, import.meta.url), "utf8");
    assert.doesNotMatch(source, /toLocale(Date|Time)?String\(|Intl\.DateTimeFormat/, file);
  }
  const preferences = readFileSync(new URL("../src/settings/preferences.ts", import.meta.url), "utf8");
  assert.match(preferences, /radioCard\("calendar"/);
});
