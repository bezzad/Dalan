// Rules: CLI-64, CLI-67
import assert from "node:assert/strict";
import { test } from "node:test";
import { botDone, siteDone, stepStates } from "../src/steps.ts";
import { defaultSiteName } from "../src/appGuide.ts";

test("CLI-64 the first step not done is the current one; steps before are done, after are todo", () => {
  assert.deepEqual(stepStates([true, false, true, false]), ["done", "current", "done", "todo"]);
  assert.deepEqual(stepStates([false, false]), ["current", "todo"]);
  assert.deepEqual(stepStates([true, true]), ["done", "done"]);
});

const builder = { builder: "approved" as const, admin: false, has_phone: true };

test("CLI-64 site guide: a new builder has only the login done", () => {
  assert.deepEqual(siteDone({ user: builder, keys: 0, sitesOnline: 0 }), [true, false, false, false, false]);
});

test("CLI-64 site guide: a user with a key but no agent has install and try still to do", () => {
  const done = siteDone({ user: builder, keys: 1, sitesOnline: 0 });
  assert.deepEqual(done, [true, false, true, false, false]);
  assert.deepEqual(stepStates(done), ["done", "current", "done", "todo", "todo"]);
});

test("CLI-64 site guide: an online site means the agent is installed, logged in and tried", () => {
  const done = siteDone({ user: builder, keys: 1, sitesOnline: 1 });
  assert.deepEqual(done, [true, true, true, true, false]);
  assert.equal(stepStates(done).indexOf("current"), 4);
});

test("CLI-64 site guide: a user who is not a builder yet has nothing done", () => {
  const user = { builder: "none" as const, admin: false, has_phone: false };
  assert.deepEqual(siteDone({ user, keys: 0, sitesOnline: 0 }), [false, false, false, false, false]);
});

test("CLI-64 bot guide: logged in, phone, builder and a bot are read from the server", () => {
  assert.deepEqual(botDone({ user: { builder: "none", admin: false, has_phone: false }, bots: 0 }), [true, false, false, false]);
  assert.deepEqual(botDone({ user: { builder: "none", admin: false, has_phone: true }, bots: 0 }), [true, true, false, false]);
  assert.deepEqual(botDone({ user: builder, bots: 0 }), [true, true, true, false]);
  assert.deepEqual(botDone({ user: builder, bots: 2 }), [true, true, true, true]);
});

test("CLI-67 the default site name comes from the username, else the name, else mysite", () => {
  assert.equal(defaultSiteName({ username: "Sara_Rad", name: "Sara" }), "sararad");
  assert.equal(defaultSiteName({ username: null, name: "Sara Rad" }), "sararad");
  assert.equal(defaultSiteName({ username: null, name: "سارا" }), "mysite");
  assert.equal(defaultSiteName({ username: "9lives", name: "Shopper" }), "shopper");
  assert.equal(defaultSiteName({ username: "ab", name: "x" }), "mysite");
  assert.equal(defaultSiteName({ username: "a".repeat(40), name: "x" }), "a".repeat(20));
});
