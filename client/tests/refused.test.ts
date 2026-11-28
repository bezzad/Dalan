// Rules: CLI-23, CLI-84
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { texts } from "../src/i18n.ts";
import { builderView } from "../src/panels.ts";

const source = (file: string) => readFileSync(new URL(file, import.meta.url), "utf8");

test("CLI-84 a refused user sees why", () => {
  assert.equal(builderView({ builder: "refused", admin: false, has_phone: true }), "refused");
  assert.equal(builderView({ builder: "refused", admin: false, has_phone: false }), "phone");
  // CLI-23: no request any more, on My bots and in the Builder section.
  for (const file of ["../src/builder.ts", "../src/settings/building.ts"]) {
    assert.match(source(file), /t\("builderRefused"\)/, file);
    assert.doesNotMatch(source(file), /askBuilder|builderWaiting/, file);
  }
  assert.doesNotMatch(texts.en.builderRefused, /ask again/);
});

test("CLI-84 the admin screen says refused and can approve", () => {
  const admin = source("../src/admin.ts");
  // CLI-93: the builder state is a cell of the users table and a line of the user's page.
  assert.match(source("../src/adminTables.ts"), /user\.builder === "refused" \? "refused"/);
  assert.match(admin, /user\.builder === "refused" \? \[action\("approve"/);
  for (const key of ["refused", "builderRefused"] as const) assert.notEqual(texts.fa[key], texts.en[key], key);
});

test("CLI-23 no phone: My bots and the Builder section say to verify the mobile, with its button", () => {
  for (const file of ["../src/builder.ts", "../src/settings/building.ts"]) {
    assert.match(source(file), /view === "phone"/, file);
    assert.match(source(file), /t\("phoneRequired"\)/, file);
    assert.match(source(file), /t\("verifyPhone"\)/, file);
  }
});

test("CLI-23 a verified phone: the Builder section says they can build, with a link to My bots", () => {
  const section = source("../src/settings/building.ts");
  assert.match(section, /view === "bots" \? h\("p", \{ class: "note" \}, t\("builderCan"\)\)/);
  assert.match(section, /open\(t\("myBots"\), "\/bots"\)/);
  assert.match(texts.en.builderStateHelp, /verified mobile/);
  // Identity:AutoBuilder off: a phone that is not a builder gets a note, with no button.
  assert.match(section, /view === "closed" \? h\("p", \{ class: "note" \}, t\("builderClosed"\)\)/);
  assert.match(source("../src/builder.ts"), /view === "closed"/);
  for (const key of ["builderCan", "builderClosed", "builderStateHelp", "phoneRequired", "verifyPhone"] as const)
    assert.notEqual(texts.fa[key], texts.en[key], key);
});
