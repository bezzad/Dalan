// Rules: CLI-23, CLI-24, CLI-25
import assert from "node:assert/strict";
import { test } from "node:test";
import { ApiError } from "../src/caller.ts";
import { errorText } from "../src/app.ts";
import { texts } from "../src/i18n.ts";
import { BuilderList, builderView, cleanValue, groupSettings, utc, type Setting } from "../src/panels.ts";

test("CLI-23 the account screen shows the request button, the waiting note or the bots button", () => {
  assert.equal(builderView({ builder: "none", admin: false }), "request");
  assert.equal(builderView({ builder: "requested", admin: false }), "waiting");
  assert.equal(builderView({ builder: "approved", admin: false }), "bots");
  assert.equal(builderView({ builder: "none", admin: true }), "bots");
});

test("CLI-24 a new token is shown once and never kept in the list", () => {
  const list = new BuilderList();

  list.created({ id: 5, username: "weather_bot", name: "Weather", token: "5:secret" }, new Date("2026-11-17T12:00:00Z"));
  assert.equal(list.shownToken?.token, "5:secret");
  assert.equal(JSON.stringify(list.bots).includes("secret"), false);

  list.closeToken();
  assert.equal(list.shownToken, null);
  assert.deepEqual(list.bots.map(b => b.username), ["weather_bot"]);
});

test("CLI-25 settings are grouped by section", () => {
  const setting = (key: string): Setting => ({ key, value: "1", start: "1", changed: false, kind: "number", restart: false });

  const groups = groupSettings([setting("Relay:MaxTextLength"), setting("Admin:PageSize"), setting("Relay:BotOnlineSeconds")]);

  assert.deepEqual(groups.map(([name, list]) => [name, list.map(s => s.key)]),
    [["Admin", ["Admin:PageSize"]], ["Relay", ["Relay:MaxTextLength", "Relay:BotOnlineSeconds"]]]);
});

test("CLI-25 a value is checked by its kind before it is sent", () => {
  assert.equal(cleanValue("number", "abc"), null);
  assert.equal(cleanValue("number", "0"), null);
  assert.equal(cleanValue("number", "-3"), null);
  assert.equal(cleanValue("number", " 42 "), "42");
  assert.equal(cleanValue("switch", "yes"), null);
  assert.equal(cleanValue("switch", "false"), "false");
  assert.equal(cleanValue("text", "fcm.googleapis.com"), "fcm.googleapis.com");
});

test("CLI-25 access log times are shown in UTC", () => {
  assert.equal(utc("2026-11-17T12:00:05.123+00:00"), "2026-11-17 12:00:05 UTC");
});

test("CLI-25 a suspended bot is named in the chat in both languages", () => {
  const key = errorText(new ApiError(403, "bot_suspended"));

  assert.equal(key, "botSuspended");
  assert.ok(texts.fa[key].length > 0 && texts.en[key].length > 0);
});
