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

test("CLI-35 first open follows the device, and nothing is kept yet", () => {
  for (const deviceDark of [false, true]) {
    const { root, storage } = fakes();
    const theme = new Theme(root, storage, deviceDark);
    assert.equal(theme.current, deviceDark ? "dark" : "light");
    assert.equal(root.dataset["theme"], theme.current);
    assert.equal(storage.getItem("dalan.theme"), null);
  }
});

test("CLI-35 the button switches only light and dark, and the choice is kept", () => {
  const { root, storage } = fakes();
  const theme = new Theme(root, storage, false);
  theme.toggle();
  assert.equal(root.dataset["theme"], "dark");
  theme.toggle();
  assert.equal(root.dataset["theme"], "light");
  theme.toggle();
  assert.equal(root.dataset["theme"], "dark");

  // The kept choice wins over the device on the next open.
  const nextOpen = { dataset: {} as Record<string, string | undefined> };
  assert.equal(new Theme(nextOpen, storage, false).current, "dark");
  assert.equal(nextOpen.dataset["theme"], "dark");
});

test("CLI-35 an old kept \"system\" choice follows the device", () => {
  const { root, storage } = fakes();
  storage.setItem("dalan.theme", "system");
  assert.equal(new Theme(root, storage, true).current, "dark");
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
