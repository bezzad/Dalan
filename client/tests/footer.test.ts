// Rules: CLI-72, CLI-73
// The copyright and version line under the footer columns, and the footer at the bottom of a short page.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { footerLine } from "../src/pages.ts";

const now = new Date(2026, 10, 25);

test("CLI-72 the English line is the sign, this year, the name and the version", () => {
  const line = footerLine("en", "v1.1.0", now);
  assert.equal(`${line.text} ${line.version}`, "© 2026 Dalan v1.1.0");
});

test("CLI-72 the Persian line has Persian digits in the year and the Persian name; the version stays Latin", () => {
  const line = footerLine("fa", "v1.1.3", now);
  assert.equal(line.text, "© ۲۰۲۶ دالان");
  assert.equal(line.version, "v1.1.3");
});

test("CLI-72 without a version (no answer, or a local run) the line has none", () => {
  for (const version of [null, undefined, "dev", "8b92fba39af3"]) assert.equal(footerLine("en", version, now).version, "", String(version));
});

test("CLI-72 the page reads the version from the public settings, never from the private /health", () => {
  const page = readFileSync(new URL("../src/public.ts", import.meta.url), "utf8");
  assert.match(page, /api\.publicSettings\(\)\)\.then\(settings => drawCopyright\(settings\.version\)/);
  assert.doesNotMatch(page, /api\.health/);
});

test("CLI-73 the footer sits at the end of a column that fills the screen, and is never fixed or sticky", () => {
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  assert.match(css, /main\.public > \.scroller \{[^}]*flex: 1 0 auto/);
  assert.match(css, /main\.public \{[^}]*min-height: 100dvh/);
  const footer = /\nfooter\.site \{([^}]*)\}/.exec(css)![1]!;
  assert.match(footer, /margin-block-start: auto/);
  assert.doesNotMatch(footer, /position: (fixed|sticky)/);
});
