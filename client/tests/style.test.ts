// Rules: CLI-71, CLI-48, CLI-13, CLI-07, CLI-58, CLI-76
// Reads the style sheet: message sizes like Telegram Desktop, and button colours readable in both themes.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");

/** The tokens of the first block that starts with `selector {`. */
function tokens(selector: string): Map<string, string> {
  const start = css.indexOf(`${selector} {`);
  assert.ok(start >= 0, selector);
  const body = css.slice(start, css.indexOf("}", start));
  return new Map([...body.matchAll(/(--[a-z-]+):\s*([^;]+);/g)].map(m => [m[1]!, m[2]!.trim()]));
}

const light = tokens(":root");
const dark = tokens(':root[data-theme="dark"]');
const darkAuto = tokens(':root:not([data-theme="light"])');

/** WCAG relative luminance of #rgb or #rrggbb. */
function luminance(color: string): number {
  const hex = color.length === 4 ? `#${[...color.slice(1)].map(c => c + c).join("")}` : color;
  const [r, g, b] = [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16) / 255).map(c => c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
  return 0.2126 * r! + 0.7152 * g! + 0.0722 * b!;
}
const contrast = (a: string, b: string) => {
  const [x, y] = [luminance(a), luminance(b)].sort((p, q) => q - p);
  return (x! + 0.05) / (y! + 0.05);
};

test("CLI-48 message text, line, padding and time like Telegram Desktop", () => {
  assert.equal(light.get("--message-text"), "14px");
  assert.equal(light.get("--message-line"), "1.35");
  assert.equal(light.get("--message-pad-block"), "6px");
  assert.equal(light.get("--message-pad-inline"), "10px");
  assert.equal(light.get("--message-time"), "12px");
  assert.match(css, /\.messages li \{[^}]*font-size: var\(--message-text\)/);
  assert.match(css, /\.messages time \{[^}]*font-size: var\(--message-time\)/);
});

test("CLI-13 every button style has readable text in light and dark (WCAG AA)", () => {
  for (const theme of [light, dark, darkAuto]) {
    for (const [ground, text] of [["--brand", "--on-brand"], ["--danger", "--on-danger"], ["--primary", "--on-primary"]] as const) {
      const ratio = contrast(theme.get(ground) ?? light.get(ground)!, theme.get(text) ?? light.get(text)!);
      assert.ok(ratio >= 4.5, `${ground} on ${text}: ${ratio.toFixed(2)}`);
    }
  }
  assert.deepEqual([...dark].filter(([name]) => name.startsWith("--on-") || name === "--primary"),
    [...darkAuto].filter(([name]) => name.startsWith("--on-") || name === "--primary"), "both dark blocks agree");
});

test("CLI-07 a keyboard row never wraps, and its buttons share the width", () => {
  assert.match(css, /\.keyboard \.row\.keys \{ flex-wrap: nowrap; \}/);
  assert.match(css, /\.keyboard \.row\.keys > button \{ flex: 1 1 0;/);
});

test("CLI-58 each line of a message and of a blockquote takes its own direction; code stays left to right", () => {
  for (const selector of [".messages p", ".messages blockquote"]) {
    const start = css.indexOf(`${selector} {`);
    assert.ok(start >= 0, selector);
    const body = css.slice(start, css.indexOf("}", start));
    assert.match(body, /unicode-bidi: plaintext;/, selector);
    assert.match(body, /text-align: start;/, selector);
  }
  assert.match(css, /\.messages pre \{[^}]*unicode-bidi: isolate;[^}]*direction: ltr;[^}]*text-align: left;/);
});

test("CLI-71 plain buttons have a visible border in the light theme", () => {
  const edge = light.get("--control-line")!;
  assert.ok(contrast(edge, light.get("--surface")!) >= 3, "against --surface");
  assert.ok(contrast(edge, light.get("--bg")!) >= 3, "against --bg");
  assert.match(css, /\nbutton, a\.button \{[^}]*border: 1px solid var\(--control-line\)/);
  assert.doesNotMatch(css, /\.login \.button\.way \{[^}]*border-color: var\(--line\)/);
  assert.equal(dark.get("--control-line"), dark.get("--line"));
  assert.equal(darkAuto.get("--control-line"), darkAuto.get("--line"));
});

test("CLI-76 section labels and small text on public pages are readable, with tokens for both languages", () => {
  assert.equal(light.get("--text-label"), "14px");
  assert.equal(light.get("--text-small"), "13px");
  const fa = tokens(":lang(fa)");
  assert.equal(fa.get("--text-label"), "15px");
  assert.equal(fa.get("--text-small"), "14px");
  for (const selector of [".label", ".soon", ".tag", ".frame-label", "ul.guards", ".branch .box"]) {
    const start = css.indexOf(`\n${selector} {`);
    assert.ok(start >= 0, selector);
    const rule = css.slice(start, css.indexOf("}", start));
    const size = selector === ".label" ? "--text-label" : "--text-small";
    assert.match(rule, new RegExp(`var\\(${size}\\)`), selector);
  }
  // no public-page rule uses a literal size below the floor (code blocks keep theirs)
  const publicCss = css.slice(css.indexOf("\n.label {"));
  const small = [...publicCss.matchAll(/^([^{\n]+)\{[^}]*?font(?:-size)?:[^;}]*?\b(\d+(?:\.\d+)?)px/gm)]
    .filter(m => Number(m[2]) < 13 && !/button\.copy|\.code|pre|code/.test(m[1]!));
  assert.deepEqual(small.map(m => m[1]!.trim()), []);
});
