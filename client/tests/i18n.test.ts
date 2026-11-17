// Rule: CLI-04
import assert from "node:assert/strict";
import { test } from "node:test";
import { Language, texts } from "../src/i18n.ts";

function fakes(saved?: string) {
  const store = new Map<string, string>(saved ? [["dalan.lang", saved]] : []);
  const storage = { getItem: (key: string) => store.get(key) ?? null, setItem: (key: string, value: string) => void store.set(key, value) };
  return { root: { lang: "", dir: "" }, storage };
}

test("CLI-04 first open is Persian, right to left", () => {
  const { root, storage } = fakes();
  const language = new Language(root, storage);

  assert.deepEqual([language.current, root.lang, root.dir], ["fa", "fa", "rtl"]);
  assert.equal(language.t("send"), "ارسال");
});

test("CLI-04 switching to English is left to right and is remembered", () => {
  const { root, storage } = fakes();
  new Language(root, storage).toggle();

  assert.deepEqual([root.lang, root.dir], ["en", "ltr"]);

  const nextOpen = { lang: "", dir: "" };
  const language = new Language(nextOpen, storage);
  assert.deepEqual([language.current, nextOpen.dir, language.t("send")], ["en", "ltr", "Send"]);
});

test("CLI-04 switching back to Persian is remembered too", () => {
  const { root, storage } = fakes("en");
  new Language(root, storage).toggle();

  assert.equal(new Language({ lang: "", dir: "" }, storage).current, "fa");
});

test("CLI-04 every text exists in both languages and none is empty", () => {
  assert.deepEqual(Object.keys(texts.en).sort(), Object.keys(texts.fa).sort());
  for (const list of Object.values(texts))
    for (const [key, text] of Object.entries(list)) assert.notEqual(text.trim(), "", `text "${key}" is empty`);
});
