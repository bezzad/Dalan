// Rules: CLI-70, SEC-15
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import type { MergePlan } from "../src/api.ts";
import { ApiError } from "../src/caller.ts";
import { errorText } from "../src/app.ts";
import { texts, type TextKey } from "../src/i18n.ts";
import { mergeRefusal, mergeSides, mergeTitle } from "../src/merge.ts";

const en = (key: TextKey) => texts.en[key];
const source = (file: string) => readFileSync(new URL(file, import.meta.url), "utf8");

const plan: MergePlan = {
  way: "phone",
  keep: "other",
  this: { id: 9, name: "Sara", bots: 0, sites: 0, keys: 0, chats: 3 },
  other: { id: 4, name: "Sara phone", bots: 2, sites: 1, keys: 0, chats: 1 },
  refusal: null,
  field: null,
  needs_code: false,
};

test("CLI-70 the question names the way, the kept account and what each side has", () => {
  assert.equal(mergeTitle(plan, en), "This mobile number already has an account. Merge it into this account?");
  const [mine, theirs] = mergeSides(plan, en);
  assert.equal(mine!.title, "This account: Sara");
  assert.equal(mine!.fate, en("mergeGone"));
  assert.equal(theirs!.counts, "2 bots, 1 sites, 0 keys, 1 chats");
  assert.equal(theirs!.fate, en("mergeKept"));
  assert.match(en("mergeNote"), /new chat id/);
  assert.equal(mergeRefusal(plan, en), null);
});

test("CLI-70 a refusal has its own text and the question then has no Merge button", () => {
  assert.equal(mergeRefusal({ ...plan, refusal: "both_have_products" }, en), en("bothHaveProducts"));
  assert.match(mergeRefusal({ ...plan, refusal: "merge_conflict", field: "email" }, en)!, /different email address/);
  assert.match(source("../src/settings/mergeCard.ts"), /\.\.\.\(refusal \? \[\] : \[mergeButton\]\)/);
});

test("CLI-70 the settings area links an email and GitHub, and asks the merge after merge_needed", () => {
  const settings = (file: string) => source(`../src/settings/${file}.ts`);
  assert.match(settings("profile"), /api\.addEmail\(/);
  assert.match(settings("security"), /href: "\/api\/account\/github"/);
  assert.match(settings("mergeCard"), /failure\.code === "merge_needed"/);
  assert.match(settings("security"), /query\.get\("merge"\) === "1"/);
  assert.match(settings("mergeCard"), /api\.dropMerge\(\)/);
});

test("SEC-15 a merge with an admin asks the admin's code in a code box and sends it", () => {
  const card = source("../src/settings/mergeCard.ts");
  assert.match(card, /plan\.needs_code \?/);
  assert.match(card, /inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code"/);
  assert.match(card, /api\.merge\(code \? latinDigits\(code\.value\.trim\(\)\) : undefined\)/);
  assert.match(source("../src/api.ts"), /merge: \(code\?: string\) => call<User>\("POST", "\/api\/account\/merge", code \? \{ code \} : undefined\)/);
  for (const key of ["mergeCodeNote", "mergeCode"] as const) assert.notEqual(texts.fa[key], texts.en[key], key);
  // A wrong or missing code (403 admin_merge) says the code is needed; two admins cannot be merged at all.
  assert.match(en("adminMerge"), /code/);
  assert.equal(mergeRefusal({ ...plan, refusal: "admin" }, en), en("adminMergeBoth"));
  assert.notEqual(texts.fa.adminMergeBoth, texts.en.adminMergeBoth);
});

test("CLI-70 the texts exist in Persian and in English", () => {
  for (const code of ["merge_needed", "has_email", "has_github", "no_merge", "both_have_products"]) {
    const key = errorText(new ApiError(409, code));
    assert.notEqual(key, "failed", code);
    assert.notEqual(texts.fa[key], texts.en[key], code);
  }
  for (const key of ["mergeTitle", "mergeNote", "merge", "addEmail", "linkGithub"] as const)
    assert.notEqual(texts.fa[key], texts.en[key], key);
});
