// Rules: CLI-39
import assert from "node:assert/strict";
import { test } from "node:test";
import { ApiError } from "../src/api.ts";
import { errorText } from "../src/app.ts";
import { httpCommand, installCommands, loginCommand, serviceCommands } from "../src/appGuide.ts";
import { builderView } from "../src/panels.ts";

test("CLI-39 the guide runs one site and two sites", () => {
  assert.equal(httpCommand([{ port: 3000, name: "levelwise" }]), "dalan http 3000 --name levelwise");
  assert.equal(httpCommand([{ port: 3000, name: "levelwise" }, { port: 8080, name: "shop" }]),
    "dalan http 3000 --name levelwise 8080 --name shop");
});

test("CLI-39 the guide keeps the site online with the service and checks it with status", () => {
  assert.equal(serviceCommands.install, "dalan service install");
  assert.equal(serviceCommands.status, "dalan status");
});

test("CLI-39 a name that cannot be reserved says why", () => {
  assert.equal(errorText(new ApiError(409, "name_taken")), "nameTaken");
  assert.equal(errorText(new ApiError(400, "bad_name")), "badSiteName");
  assert.equal(errorText(new ApiError(409, "site_limit")), "siteLimit");
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
