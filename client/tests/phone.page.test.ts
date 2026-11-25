// Rules: CLI-68, CLI-69
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { startsAtTop } from "../src/next.ts";

const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

test("CLI-68 a link to another page scrolls to the top, a link with a #part does not", () => {
  assert.equal(startsAtTop("/about"), true);
  assert.equal(startsAtTop("/guide/site"), true);
  assert.equal(startsAtTop("/#plans"), false);
});

test("CLI-68 going to another address scrolls the window to the top", () => {
  const main = read("../src/main.ts");
  assert.match(main, /startsAtTop\(path\)\) window\.scrollTo\(0, 0\)/);
});

test("CLI-69 the viewport has scale 1 and no larger scale", () => {
  const html = read("../public/index.html");
  assert.match(html, /name="viewport" content="[^"]*initial-scale=1[^"]*maximum-scale=1/);
});

test("CLI-69 touch-action is manipulation and every field is at least 16px", () => {
  const css = read("../public/app.css");
  assert.match(css, /html \{ touch-action: manipulation; \}/);
  assert.match(css, /input, textarea, select \{ font-size: 16px; \}/);
});
