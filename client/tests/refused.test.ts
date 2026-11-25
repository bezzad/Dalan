// Rules: CLI-23, CLI-84
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { texts } from "../src/i18n.ts";
import { builderView } from "../src/panels.ts";

const source = (file: string) => readFileSync(new URL(file, import.meta.url), "utf8");

test("CLI-84 a refused user sees why and may ask again", () => {
  assert.equal(builderView({ builder: "refused", admin: false, has_phone: true }), "refused");
  assert.equal(builderView({ builder: "refused", admin: false, has_phone: false }), "phone");
  const account = source("../src/account.ts");
  assert.match(account, /view === "refused" \? \[h\("p", \{ class: "note" \}, t\("builderRefused"\)\)\]/);
  assert.match(account, /view === "request" \|\| view === "refused"/);
});

test("CLI-84 the admin screen says refused and can approve", () => {
  const admin = source("../src/admin.ts");
  assert.match(admin, /user\.builder === "refused" \? "refused"/);
  assert.match(admin, /user\.builder === "refused" \? \[action\("approve"/);
  for (const key of ["refused", "builderRefused"] as const) assert.notEqual(texts.fa[key], texts.en[key], key);
});

test("CLI-23 the other states still show what they showed", () => {
  assert.equal(builderView({ builder: "none", admin: false, has_phone: true }), "request");
  assert.equal(builderView({ builder: "requested", admin: false, has_phone: true }), "waiting");
  assert.equal(builderView({ builder: "approved", admin: false, has_phone: true }), "bots");
});
