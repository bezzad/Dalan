// Rule: DEL-09
// The files a user runs on their own computer take everything from Dalan: no GitHub, npm, NuGet or CDN address.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const files = ["../../src/Sites/Dalan.Sites/Downloads/install.sh", "../../src/Sites/Dalan.Sites/Downloads/install.ps1", "../public/dalan.mjs"];

test("DEL-09 the install scripts and the builder CLI name no host except the Dalan address given to them", () => {
  for (const file of files) {
    const text = readFileSync(new URL(file, import.meta.url), "utf8");
    const found = text.match(/https?:\/\/[^\s"'`)]+/gi) ?? [];
    for (const address of found) assert.match(address, /^https?:\/\/__DALAN_URL__/, `${file} names ${address}`);
  }
});

test("DEL-09 the agent's default server is Dalan's own host", () => {
  const source = readFileSync(new URL("../../agent/Dalan.Agent/CommandLine.cs", import.meta.url), "utf8");
  assert.match(source, /DefaultServer = "https:\/\/tunnel\.dalanet\.ir"/);
});
