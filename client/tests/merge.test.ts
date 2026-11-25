// Rules: CLI-70
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
  assert.match(source("../src/account.ts"), /\.\.\.\(refusal \? \[\] : \[mergeButton\]\)/);
});

test("CLI-70 the account screen links an email and GitHub, and asks the merge after merge_needed", () => {
  const account = source("../src/account.ts");
  assert.match(account, /api\.addEmail\(/);
  assert.match(account, /href: "\/api\/account\/github"/);
  assert.match(account, /failure\.code === "merge_needed"/);
  assert.match(account, /query\.get\("merge"\) === "1"/);
  assert.match(account, /api\.dropMerge\(\)/);
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
