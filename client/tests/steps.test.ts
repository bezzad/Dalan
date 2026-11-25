// Rules: CLI-64, CLI-65, CLI-66, CLI-67
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { botDone, siteDone, stepStates, stepsDone } from "../src/steps.ts";
import { defaultSiteName } from "../src/appGuide.ts";
import { siteGuideSteps } from "../src/guide.ts";
import { texts } from "../src/i18n.ts";

const numbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://example.ir", bot_api_url: "https://api.example.ir", contact_max_length: 2000 };
const source = (file: string) => readFileSync(new URL(file, import.meta.url), "utf8");

test("CLI-65 the key step says it is for the first site, and opens a modal on the same page", () => {
  for (const lang of ["fa", "en"] as const) {
    const steps = siteGuideSteps(lang, numbers);
    assert.equal(steps.length, 5);
    assert.equal(steps.filter(step => step.key).length, 1, lang);
    assert.equal(steps[2]!.key, true, lang);
  }
  assert.match(siteGuideSteps("en", numbers)[2]!.text, /first time you hand us a site/);
  assert.match(siteGuideSteps("fa", numbers)[2]!.text, /اولین بار است سایتی/);
  const modal = source("../src/keyModal.ts");
  assert.match(modal, /showModal\(\)/);
  assert.match(modal, /api\.makeKey/);
  assert.match(modal, /loginCommand\(/);
  assert.doesNotMatch(modal, /navigate|location|app\.go/);
  for (const key of ["keyLink", "keyLinkAgain", "keyModalNote"] as const) assert.ok(texts.fa[key] && texts.en[key], key);
});

test("CLI-64 the stepper opens the first step not done, marks done steps and has Back and Next", () => {
  const view = source("../src/stepper.ts");
  assert.match(view, /stepStates\(isDone\(\)\)/);
  assert.match(view, /t\("stepDone"\)/);
  assert.match(view, /t\("stepNext"\)/);
  assert.match(view, /t\("stepBack"\)/);
  const page = source("../src/public.ts");
  assert.match(page, /api\.accessKeys\(\)/);
  assert.match(page, /api\.mySites\(\)/);
  assert.match(page, /api\.builderBots\(\)/);
});

test("CLI-66 a visitor of the site guide gets the log in line and no steps", () => {
  const page = source("../src/public.ts");
  const guest = page.slice(page.indexOf('case "guideSite"'));
  assert.ok(guest.indexOf("guideLoginFirst") < guest.indexOf("siteGuideSteps"));
  assert.match(guest, /if \(!context\.user\)/);
  assert.match(guest, /context\.enter\(\)/);
  assert.match(texts.en.guideLoginFirst, /Log in first/);
  assert.ok(texts.fa.guideLoginFirst.length > 0);
});

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

test("CLI-64 Next does not make a step done that the server knows is not done", () => {
  // Site guide: no key, no site online; the person pressed Next on every step.
  assert.deepEqual(stepsDone([true, false, false, false, false], [true, true, true, true, false], 2, false), [true, false, false, false, false]);
  // A key made in the modal makes the key step done.
  assert.deepEqual(stepsDone([true, false, false, false, false], [false, false, false, false, false], 2, true), [true, false, true, false, false]);
  // Bot guide: steps beyond what the server knows are done only by Next.
  assert.deepEqual(stepsDone([true, true, true, true], [false, false, false, false, true, false], -1, false), [true, true, true, true, true, false]);
});
