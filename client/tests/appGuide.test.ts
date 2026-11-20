// Rules: CLI-39
import assert from "node:assert/strict";
import { test } from "node:test";
import { httpCommand, installCommands, loginCommand } from "../src/appGuide.ts";
import { builderView } from "../src/panels.ts";

test("CLI-39 the guide runs one site and two sites", () => {
  assert.equal(httpCommand([{ port: 3000, name: "levelwise" }]), "dalan http 3000 --name levelwise");
  assert.equal(httpCommand([{ port: 3000, name: "levelwise" }, { port: 8080, name: "shop" }]),
    "dalan http 3000 --name levelwise 8080 --name shop");
});

test("CLI-39 the login command has the key and the server of this Dalan", () => {
  assert.equal(loginCommand("dalan_abc", "http://localhost:5000"), "dalan login dalan_abc --server http://localhost:5000");
});

test("CLI-39 the install is one line from Dalan's own site", () => {
  const install = installCommands("https://dalanet.ir");
  assert.equal(install.unix, "curl -fsSL https://dalanet.ir/install.sh | sh");
  assert.equal(install.windows, "irm https://dalanet.ir/install.ps1 | iex");
});

test("CLI-39 a user who is not a builder gets the way to become one, a builder gets the list", () => {
  assert.notEqual(builderView({ builder: "none", admin: false, has_phone: true }), "bots");
  assert.notEqual(builderView({ builder: "requested", admin: false, has_phone: true }), "bots");
  assert.equal(builderView({ builder: "approved", admin: false, has_phone: true }), "bots");
});
