// Rule: CLI-03
// Scans the built client (run "npm run build" first). Nothing in it may name another host.
import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import { test } from "node:test";

const dist = new URL("../dist/", import.meta.url);
const textFile = /\.(html|css|js|json|webmanifest|svg)$/;
// The XML namespace of SVG is a name, not an address that is loaded.
const allowed = ["http://www.w3.org/2000/svg"];

test("CLI-03 no built client file names a foreign host", () => {
  const files = readdirSync(dist, { recursive: true, encoding: "utf8" }).filter(file => textFile.test(file));
  assert.ok(files.includes("index.html") && files.includes("main.js"), "the client is not built: run npm run build");

  for (const file of files) {
    let text = readFileSync(new URL(file, dist), "utf8");
    for (const name of allowed) text = text.replaceAll(name, "");
    // "http://x", "https://x", and "//x" at the start of an address (after a quote, a bracket or a space).
    const found = text.match(/https?:\/\/[^\s"'`)]+|(?<=["'`(=\s])\/\/[a-z0-9][^\s"'`)]*/gi);
    assert.equal(found, null, `${file} names another host: ${found?.join(", ")}`);
  }
});

test("CLI-03 the page loads its script, style and font from its own origin", () => {
  const html = readFileSync(new URL("index.html", dist), "utf8");
  const addresses = [...html.matchAll(/(?:src|href)="([^"]+)"/g)].map(match => match[1]!);

  assert.ok(addresses.length > 0);
  for (const address of addresses) assert.match(address, /^\/[^/]/, `"${address}" must start with one slash`);
  assert.match(readFileSync(new URL("app.css", dist), "utf8"), /url\("\/fonts\/vazirmatn\.woff2"\)/);
});
