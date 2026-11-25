// Rule: CLI-63
import assert from "node:assert/strict";
import { test } from "node:test";
import { loginTarget, rememberNext, safeNext, takeNext } from "../src/next.ts";

function storage(): Storage {
  const saved = new Map<string, string>();
  return {
    getItem: key => saved.get(key) ?? null,
    setItem: (key, value) => void saved.set(key, value),
    removeItem: key => void saved.delete(key),
  } as Storage;
}

test("CLI-63 an internal path is accepted", () => {
  assert.equal(safeNext("/guide/site"), "/guide/site");
  assert.equal(safeNext("/bots/new?x=1"), "/bots/new?x=1");
  assert.equal(safeNext("/@weather_bot"), "/@weather_bot");
});

test("CLI-63 anything that can leave the site is ignored", () => {
  for (const bad of ["//evil.example", "https://evil.example", "/\\evil.example", "javascript:alert(1)", "evil", "", "/a\\b",
    "/a\nb", "/" + "x".repeat(200), null, undefined]) {
    assert.equal(safeNext(bad), null, String(bad));
  }
});

test("CLI-63 the kept path is taken once and cleared", () => {
  const store = storage();
  rememberNext("/guide/site", store);
  assert.equal(takeNext(store), "/guide/site");
  assert.equal(takeNext(store), null);
});

test("CLI-63 a bad path is not kept", () => {
  const store = storage();
  rememberNext("//evil.example", store);
  assert.equal(takeNext(store), null);
});

test("CLI-63 storage that fails keeps nothing and never throws", () => {
  const broken = { getItem() { throw new Error("no"); }, setItem() { throw new Error("no"); }, removeItem() { throw new Error("no"); } } as unknown as Storage;
  rememberNext("/guide/site", broken);
  assert.equal(takeNext(broken), null);
});

test("CLI-63 the login goes to ?next=, else the kept path, else stays, and from / goes to My bots", () => {
  assert.equal(loginTarget("/", "?next=/guide/site", null), "/guide/site");
  assert.equal(loginTarget("/", "", "/apps"), "/apps");
  assert.equal(loginTarget("/", "?next=/guide/site", "/apps"), "/guide/site");
  assert.equal(loginTarget("/", "?next=//evil.example", null), "/bots");
  assert.equal(loginTarget("/", "", null), "/bots");
  assert.equal(loginTarget("/guide/site", "", null), "/guide/site");
  assert.equal(loginTarget("/@weather_bot", "", null), "/@weather_bot");
});
