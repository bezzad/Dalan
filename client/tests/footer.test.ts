// Rules: CLI-72, CLI-73
// The copyright and version line under the footer columns, and the footer at the bottom of a short page.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { footerLine } from "../src/pages.ts";

const live = { version: "v26.11.25.114726", commit: "7207d600e166" };
const now = new Date(2030, 0, 1);

test("CLI-72 the English line is the sign, the year of the version and the name; the tooltip is the commit", () => {
  const line = footerLine("en", live, now);
  assert.equal(line.text, "© 2026 Dalan");
  assert.equal(line.version, "v26.11.25.114726");
  assert.equal(line.commit, "7207d600e166");
});

test("CLI-72 the Persian line has Persian digits in the year and the Persian name", () => {
  const line = footerLine("fa", live, now);
  assert.equal(line.text, "© ۲۰۲۶ دالان");
  assert.equal(line.version, "v26.11.25.114726");
});

test("CLI-72 without an answer from /health the line has the current year and no version", () => {
  for (const [lang, text] of [["en", "© 2030 Dalan"], ["fa", "© ۲۰۳۰ دالان"]] as const) {
    const line = footerLine(lang, null, now);
    assert.equal(line.text, text);
    assert.equal(line.version, "");
  }
});

test("CLI-72 a local run (version dev) has the current year", () => {
  assert.equal(footerLine("en", { version: "dev", commit: "" }, now).text, "© 2030 Dalan");
});

test("CLI-73 the footer sits at the end of a column that fills the screen, and is never fixed or sticky", () => {
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  assert.match(css, /main\.public > \.scroller \{[^}]*flex: 1 0 auto/);
  assert.match(css, /main\.public \{[^}]*min-height: 100dvh/);
  const footer = /\nfooter\.site \{([^}]*)\}/.exec(css)![1]!;
  assert.match(footer, /margin-block-start: auto/);
  assert.doesNotMatch(footer, /position: (fixed|sticky)/);
});
