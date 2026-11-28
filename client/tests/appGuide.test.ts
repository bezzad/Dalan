// Rules: CLI-39
import assert from "node:assert/strict";
import { test } from "node:test";
import { ApiError } from "../src/api.ts";
import { errorText } from "../src/app.ts";
import { httpCommand, installCommands, loginCommand, serviceCommands } from "../src/appGuide.ts";
import { siteGuideSteps } from "../src/guide.ts";
import { texts } from "../src/i18n.ts";
import { builderView } from "../src/panels.ts";

test("CLI-39 the guide runs one site and two sites", () => {
  assert.equal(httpCommand([{ port: 3000, name: "mysite" }]), "dalan http 3000 --name mysite");
  assert.equal(httpCommand([{ port: 3000, name: "mysite" }, { port: 8080, name: "shop" }]),
    "dalan http 3000 --name mysite 8080 --name shop");
});

test("CLI-39 the guide keeps the site online with the service and checks it with status", () => {
  assert.equal(serviceCommands.install, "dalan service install");
  assert.equal(serviceCommands.status, "dalan status");
  assert.equal(serviceCommands.uninstall, "dalan service uninstall");
});

test("CLI-39 the service step says Windows starts it at log-on, where the log is and how to remove it (AGT-14)", () => {
  const numbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
    site_url: "https://dalanet.ir", bot_api_url: "https://api.dalanet.ir", contact_max_length: 2000 };
  for (const lang of ["fa", "en"] as const) {
    const keep = siteGuideSteps(lang, numbers).at(-1)!.text;
    for (const text of [keep, texts[lang].serviceWindowsNote]) {
      assert.match(text, /%APPDATA%\\dalan\\dalan\.log/, `${lang}: ${text}`);
      assert.match(text, /dalan service uninstall/, `${lang}: ${text}`);
      assert.match(text, lang === "fa" ? /ویندوز/ : /log on/, `${lang}: ${text}`);
    }
  }
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
