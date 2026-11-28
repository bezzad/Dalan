// Rules: CLI-96
// The bell: the unread badge, which links a notice may open, which notice the panel marks read, and where it sits.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { badgeText, nextBefore, noticeLink, readUpTo } from "../src/inbox.ts";
import { texts } from "../src/i18n.ts";
import type { Notice } from "../src/api.ts";

const source = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

test("CLI-96 the badge shows the unread count, none at 0", () => {
  assert.equal(badgeText(0), "");
  assert.equal(badgeText(2), "2");
  assert.equal(badgeText(99), "99");
  assert.equal(badgeText(150), "99+");
});

test("CLI-96 a notice opens only a path of this site", () => {
  assert.equal(noticeLink("/dashboard/admin/users/7"), "/dashboard/admin/users/7");
  assert.equal(noticeLink(null), null);
  assert.equal(noticeLink(""), null);
  assert.equal(noticeLink("//evil.example/x"), null);
  assert.equal(noticeLink("https://evil.example/x"), null);
  assert.equal(noticeLink("/\\evil.example"), null);
  assert.equal(noticeLink("javascript:alert(1)"), null);
});

test("CLI-96 opening the panel marks read up to the newest shown; More asks before the oldest shown", () => {
  const notices: Notice[] = [
    { id: 9, title: "a", text: "", link: null, at: "2026-11-27T10:00:00Z", read: false },
    { id: 4, title: "b", text: "", link: null, at: "2026-11-26T10:00:00Z", read: true },
  ];

  assert.equal(readUpTo(notices), 9);
  assert.equal(readUpTo([]), null);
  assert.equal(readUpTo([{ ...notices[0]!, read: true }]), null);
  assert.equal(nextBefore(notices), 4);
  assert.equal(nextBefore([]), null);
});

test("CLI-96 the bell is in the header of every signed-in page, and refreshes on each screen change", () => {
  const main = source("../src/main.ts");
  // The site header tools and the chat header both get it.
  assert.match(main, /function tools\(\)[\s\S]*?bell\(/);
  assert.match(main, /chatScreen\(app, username, \[[^\]]*bell\(/);
  // Only a signed-in user has one.
  assert.match(main, /function bell\(\)[\s\S]*?if \(!app\)/);
  const inbox = source("../src/inbox.ts");
  assert.match(inbox, /api\.inbox\(/);
  assert.match(inbox, /api\.readInbox\(/);
  assert.match(inbox, /"aria-label"/);
});

test("CLI-96 the texts, the style and the design page", () => {
  for (const lang of ["fa", "en"] as const) {
    for (const key of ["inbox", "inboxEmpty", "inboxEmptyNote", "more"] as const) assert.ok(texts[lang][key], `${lang}.${key}`);
  }
  const css = source("../public/app.css");
  assert.match(css, /\.menu\.bell /);
  assert.match(css, /\.bell-count/);
  assert.match(source("../../docs/UI.md"), /Bell \(CLI-96\)/);
});
