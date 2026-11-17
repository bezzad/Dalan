// Rule: CLI-08
import assert from "node:assert/strict";
import { test } from "node:test";
import { Backoff } from "../src/backoff.ts";

test("CLI-08 the wait doubles after each failure", () => {
  const backoff = new Backoff(1, 30);

  assert.deepEqual([backoff.next(), backoff.next(), backoff.next()], [1, 2, 4]);
});

test("CLI-08 the wait never passes the maximum", () => {
  const backoff = new Backoff(1, 30);
  const waits = Array.from({ length: 10 }, () => backoff.next());

  assert.equal(Math.max(...waits), 30);
  assert.equal(waits.at(-1), 30);
});

test("CLI-08 a success starts the wait again at the minimum", () => {
  const backoff = new Backoff(1, 30);
  backoff.next();
  backoff.next();

  backoff.reset();

  assert.equal(backoff.next(), 1);
});
