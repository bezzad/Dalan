// Rule: CLI-35
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { Theme } from "../src/theme.ts";

function fakes() {
  const store = new Map<string, string>();
  const storage = { getItem: (key: string) => store.get(key) ?? null, setItem: (key: string, value: string) => void store.set(key, value) };
  return { root: { dataset: {} as Record<string, string | undefined> }, storage };
}

test("CLI-35 first open follows the device: no data-theme", () => {
  const { root, storage } = fakes();
  const theme = new Theme(root, storage);
  assert.equal(theme.current, "system");
  assert.equal(root.dataset["theme"], undefined);
});

test("CLI-35 the button moves light, dark, system, and the choice is kept", () => {
  const { root, storage } = fakes();
  const theme = new Theme(root, storage);
  theme.toggle();
  assert.equal(root.dataset["theme"], "light");
  theme.toggle();
  assert.equal(root.dataset["theme"], "dark");

  const nextOpen = { dataset: {} as Record<string, string | undefined> };
  assert.equal(new Theme(nextOpen, storage).current, "dark");
  assert.equal(nextOpen.dataset["theme"], "dark");

  theme.toggle();
  assert.equal(theme.current, "system");
  assert.equal(root.dataset["theme"], undefined);
});

test("CLI-35 the style sheet has dark colours for the device and for the choice, and code stays dark", () => {
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  assert.match(css, /@media \(prefers-color-scheme: dark\) \{\s*:root:not\(\[data-theme="light"\]\)/);
  assert.match(css, /:root\[data-theme="dark"\] \{/);
  // The light code background is as dark as the dark one: code blocks look the same in both themes.
  const lightCode = /:root \{[^}]*--code-bg: #([0-9a-f]{6})/.exec(css)![1]!;
  assert.ok(parseInt(lightCode.slice(0, 2), 16) < 0x30, `light --code-bg #${lightCode} must be dark`);
  assert.doesNotMatch(css, /main\.public\.dark/);
});
