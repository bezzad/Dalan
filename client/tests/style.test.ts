// Rules: CLI-71, CLI-48, CLI-13, CLI-07, CLI-58, CLI-76, CLI-94, CLI-103, CLI-104, CLI-105
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

test("CLI-94 the accent line of a big title is a five-colour gradient that moves slowly; still with reduced motion", () => {
  for (const selector of [":root", ":root[data-theme=\"dark\"]"]) {
    const rainbow = tokens(selector).get("--rainbow") ?? "";
    assert.equal(rainbow.split(",").length, 5, `${selector} --rainbow: ${rainbow}`);
  }
  const accent = /\.accent \{([^}]*)\}/.exec(css)?.[1] ?? "";
  assert.match(accent, /background-clip: text;/);
  assert.match(accent, /color: transparent;/);
  assert.match(accent, /animation: title-colors \d+s linear infinite;/);
  assert.match(css, /@keyframes title-colors \{/);
  assert.match(css, /:lang\(fa\) \.accent \{[^}]*270deg/);
  assert.match(css, /@media \(prefers-reduced-motion: reduce\) \{[^}]*\.accent \{ animation: none; \}/);
  // The hero and the closing title use it.
  const pub = readFileSync(new URL("../src/public.ts", import.meta.url), "utf8");
  assert.equal(pub.match(/h\("span", \{ class: "accent" \}/g)?.length, 2);
});

test("CLI-103 the big titles of the home page use the display font, served by Dalan; a still cloud behind the hero", () => {
  for (const [family, file] of [["Dalan Serif", "serif"], ["Dalan Naskh", "naskh"]] as const) {
    assert.match(css, new RegExp(`@font-face \\{[^}]*font-family: "${family}";[^}]*url\\("/fonts/${file}\\.woff2"\\)`), family);
  }
  assert.match(tokens(":root").get("--display") ?? "", /^"Dalan Serif", "Dalan Naskh",/);
  for (const selector of [".hero h2", ".cta h2", ".landing-title"]) {
    assert.match(new RegExp(`${selector.replace(".", "\\.")}[^{]*\\{([^}]*)\\}`).exec(css)?.[1] ?? "", /font-family: var\(--display\)/, selector);
  }
  const cloud = /\.hero::before \{([^}]*)\}/.exec(css)?.[1] ?? "";
  assert.match(cloud, /radial-gradient/);
  assert.match(cloud, /filter: blur\(/);
  assert.doesNotMatch(cloud, /animation/);
  const build = readFileSync(new URL("../build.mjs", import.meta.url), "utf8");
  assert.match(build, /dist\/fonts\/serif\.woff2/);
  assert.match(build, /dist\/fonts\/naskh\.woff2/);
});

test("CLI-104 a card's colour drives its badge, label and picture; hover tints the card and lights its button", () => {
  const tones = [...css.matchAll(/\.tone-(\w+) \{ --tone: var\(--(\w+)\); \}/g)].map(m => m[2]);
  assert.ok(tones.length >= 4, "four tones");
  assert.equal(new Set(tones).size, tones.length);
  for (const selector of [":root", ":root[data-theme=\"dark\"]"]) {
    for (const tone of tones) assert.ok(tokens(selector).get(`--${tone}`), `${selector} --${tone}`);
  }
  assert.match(css, /\.badge-icon \{[^}]*color: var\(--tone, var\(--brand\)\)/);
  assert.match(css, /\.wire \{[^}]*var\(--tone, var\(--brand\)\)/);
  assert.match(css, /\.product:hover \{[^}]*color-mix\(in srgb, var\(--tone/);
  assert.match(css, /\.product:hover a\.button \{[^}]*background: var\(--text\)/);
});

test("CLI-105 the strip of names moves slowly with faded edges; still and wrapped with reduced motion", () => {
  assert.match(css, /\.strip \.rail \{[^}]*mask-image: linear-gradient/);
  assert.match(css, /\.strip \.track \{[^}]*animation: strip \d+s linear infinite/);
  assert.match(css, /@media \(prefers-reduced-motion: reduce\) \{[^@]*\.strip \.track \{ animation: none;[^@]*\.strip ul\[aria-hidden\] \{ display: none; \}/);
});
