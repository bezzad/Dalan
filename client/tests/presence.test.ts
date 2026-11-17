// Rules: CLI-18, CLI-19, CLI-20
import assert from "node:assert/strict";
import { test } from "node:test";
import { Language } from "../src/i18n.ts";
import { ReadPosition, stamp, type Message } from "../src/messages.ts";

function language(lang: "fa" | "en"): Language {
  const storage = { getItem: () => lang, setItem: () => {} };
  return new Language({ lang: "", dir: "" }, storage);
}

const now = new Date("2026-11-17T12:00:00Z");
const ago = (minutes: number) => new Date(now.getTime() - minutes * 60000).toISOString();

test("CLI-18 the read position is sent only when it moves forward", () => {
  const read = new ReadPosition();
  assert.equal(read.next(4, true), 4);

  assert.equal(read.next(4, true), null);
  assert.equal(read.next(6, true), 6);
});

test("CLI-18 a hidden page sends no read position until it is visible again", () => {
  const read = new ReadPosition();

  assert.equal(read.next(3, false), null);
  assert.equal(read.next(3, true), 3);
});

test("CLI-19 last seen in English", () => {
  const en = language("en");

  assert.equal(en.seen({ online: false, last_seen: ago(5) }, now), "last seen 5 minutes ago");
  assert.equal(en.seen({ online: false, last_seen: ago(0) }, now), "last seen just now");
  assert.equal(en.seen({ online: false, last_seen: ago(125) }, now), "last seen 2 hours ago");
  assert.match(en.seen({ online: false, last_seen: ago(3 * 24 * 60) }, now), /^last seen 14 Nov 2026$/);
  assert.equal(en.seen({ online: true, last_seen: ago(0) }, now), "online");
  assert.equal(en.seen({ online: false, last_seen: null }, now), "");
});

test("CLI-19 last seen in Persian uses Persian words and digits", () => {
  assert.equal(language("fa").seen({ online: false, last_seen: ago(5) }, now), "آخرین بازدید ۵ دقیقه پیش");
});

test("CLI-20 a message the bot never got has the not-delivered mark at the end", () => {
  const message: Message = { id: 1, version: 2, from: "user", text: "hi", date: now.toISOString(), not_delivered: true };

  assert.equal(stamp(message, "12:00", { edited: "edited", notDelivered: "⚠ not delivered" }), "12:00 · ⚠ not delivered");
});
