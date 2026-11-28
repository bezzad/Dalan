// Rules: CLI-100
// Scrollbars like Telegram on a computer: thin, hidden until the mouse is over the part that scrolls; touch screens unchanged.
// The browser test (tests/client-e2e/scrollbar.spec.ts) checks the painted scrollbar.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
const start = css.indexOf("@media (hover: hover) and (pointer: fine) {\n  * { --thumb: transparent; }");
const block = start < 0 ? "" : css.slice(start);

test("CLI-100 only for a mouse: a touch screen keeps its own scrollbars", () => {
  assert.ok(start >= 0, "the scrollbar rules sit inside @media (hover: hover) and (pointer: fine)");
});

test("CLI-100 thin: 6px, and thin in Firefox", () => {
  assert.match(block, /::-webkit-scrollbar \{ width: 6px; height: 6px; \}/);
  assert.match(block, /scrollbar-width: thin; scrollbar-color: var\(--thumb\) transparent;/);
});

test("CLI-100 hidden on every element, seen on the hovered one or with the keyboard inside", () => {
  assert.match(block, /\* \{ --thumb: transparent; \}/);
  assert.match(block, /:hover, :focus-within \{ --thumb: color-mix\(in srgb, var\(--muted\)/);
  assert.match(block, /::-webkit-scrollbar-thumb \{ border-radius: 3px; background: var\(--thumb\); \}/);
});
