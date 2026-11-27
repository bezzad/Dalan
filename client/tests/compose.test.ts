// Rules: CLI-85, CLI-86, CLI-87, CLI-88
// The message box: Enter like Telegram, the growing box, the expand button, the marks of the big editor and a draft per chat.
import assert from "node:assert/strict";
import { test } from "node:test";
import { Drafts, enterAction, expandShown, lineCount, marked } from "../src/compose.ts";
import { closeOverlays, track } from "../src/overlays.ts";

const key = (k: string, extra: Partial<Parameters<typeof enterAction>[0]> = {}) =>
  enterAction({ key: k, shift: false, ctrl: false, meta: false, composing: false, coarse: false, ...extra });

test("CLI-85 a fine pointer: Enter sends; Shift+Enter and Ctrl+Enter add a line", () => {
  assert.equal(key("Enter"), "send");
  assert.equal(key("Enter", { shift: true }), "newline");
  assert.equal(key("Enter", { ctrl: true }), "newline");
  assert.equal(key("Enter", { meta: true }), "newline");
  assert.equal(key("a"), "none");
});

test("CLI-85 a coarse pointer: Enter adds a line and never sends", () => {
  assert.equal(key("Enter", { coarse: true }), "newline");
  assert.equal(key("Enter", { coarse: true, shift: true }), "newline");
  assert.equal(key("Enter", { coarse: true, ctrl: true }), "newline");
});

test("CLI-85 never sends while an IME is composing", () => {
  assert.equal(key("Enter", { composing: true }), "none");
  assert.equal(key("Enter", { composing: true, coarse: true }), "none");
  // Old Safari tells a composing key only by keyCode 229, with key "Process".
  assert.equal(key("Process"), "none");
});

test("CLI-85 the chat reads the pointer at each key press, never the user agent", async () => {
  const { readFileSync } = await import("node:fs");
  for (const file of ["chat.ts", "editor.ts", "compose.ts"]) {
    const source = readFileSync(new URL(`../src/${file}`, import.meta.url), "utf8");
    assert.doesNotMatch(source, /userAgent/, file);
  }
  const compose = readFileSync(new URL("../src/compose.ts", import.meta.url), "utf8");
  assert.match(compose, /matchMedia\("\(pointer: coarse\)"\)\.matches/);
});

test("CLI-86 and CLI-87 lines are counted from the height, so wrapped lines count", () => {
  assert.equal(lineCount(24, 24), 1);
  assert.equal(lineCount(72, 24), 3);
  assert.equal(lineCount(97, 24), 4);
  assert.equal(lineCount(0, 0), 1);
  assert.equal(expandShown(3), false);
  assert.equal(expandShown(4), true);
});

test("CLI-86 the box grows to about six lines (--compose-max-height), then scrolls; the buttons stay at the bottom", async () => {
  const { readFileSync } = await import("node:fs");
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  assert.match(css, /--compose-max-height: min\(30dvh, 10\.5em\);/);
  assert.match(css, /form\.compose textarea \{[^}]*max-height: var\(--compose-max-height\);[^}]*overflow-y: auto;/);
  assert.match(css, /form\.compose \{ align-items: flex-end; \}/);
  // The box's own class: ".box" is the landing page's diagram box, with a mono font and a border (owner, 2026-11-27).
  const chat = readFileSync(new URL("../src/chat.ts", import.meta.url), "utf8");
  assert.match(chat, /h\("div", \{ class: "compose-box" \}, input, expand\)/);
  assert.doesNotMatch(chat, /class: "box"/);
});

test("CLI-87 Ctrl+B and Ctrl+I put ** and _ around the selection", () => {
  assert.equal(marked("world", "bold"), "**world**");
  assert.equal(marked("world", "italic"), "_world_");
  assert.equal(marked("", "bold"), "****");
});

class Store {
  readonly map = new Map<string, string>();
  get length(): number { return this.map.size; }
  key(i: number): string | null { return [...this.map.keys()][i] ?? null; }
  getItem(k: string): string | null { return this.map.get(k) ?? null; }
  setItem(k: string, v: string): void { this.map.set(k, v); }
  removeItem(k: string): void { this.map.delete(k); }
}

test("CLI-88 each chat keeps its own draft, for this user", () => {
  const store = new Store();
  const sara = new Drafts(store, 7);
  sara.save(1, "half");
  assert.equal(sara.load(1), "half");
  assert.equal(sara.load(2), "");
  assert.equal(new Drafts(store, 8).load(1), "", "another user on this browser does not see it");
  sara.save(1, "");
  assert.equal(store.map.size, 0, "an empty box keeps no draft");
});

test("CLI-88 a send removes the draft; logout removes every draft", () => {
  const store = new Store();
  store.setItem("dalan.theme", "dark");
  const sara = new Drafts(store, 7);
  sara.save(1, "a");
  sara.save(2, "b");
  sara.remove(1);
  assert.equal(sara.load(1), "");
  new Drafts(store, 9).save(3, "c");
  Drafts.clearAll(store);
  assert.deepEqual([...store.map.keys()], ["dalan.theme"]);
});

test("CLI-88 a broken storage (private window) never breaks the chat", () => {
  const broken = { get length(): number { throw new Error("denied"); }, key: () => { throw new Error("denied"); },
    getItem: () => { throw new Error("denied"); }, setItem: () => { throw new Error("denied"); }, removeItem: () => { throw new Error("denied"); } };
  const drafts = new Drafts(broken, 1);
  drafts.save(1, "x");
  assert.equal(drafts.load(1), "");
  drafts.remove(1);
  Drafts.clearAll(broken);
});

test("CLI-88 every end of a session (logout or 401) goes through the login screen, which removes every draft", async () => {
  const { readFileSync } = await import("node:fs");
  const main = readFileSync(new URL("../src/main.ts", import.meta.url), "utf8");
  assert.match(main, /function showLogin\([^)]*\): void \{\n\s*\/\/[^\n]*\n\s*Drafts\.clearAll\(localStorage\);/);
  assert.match(main, /onLoggedOut: showLogin/);
});

test("CLI-87 a new screen closes the open editor and viewers, each once", async () => {
  const closed: string[] = [];
  const untrackA = track(() => { closed.push("editor"); untrackA(); });
  track(() => closed.push("viewer"));
  closeOverlays();
  assert.deepEqual(closed, ["editor", "viewer"]);
  const { readFileSync } = await import("node:fs");
  assert.match(readFileSync(new URL("../src/main.ts", import.meta.url), "utf8"), /function show\(element: HTMLElement\): void \{\n[^\n]*\n\s*closeOverlays\(\);/);
});
