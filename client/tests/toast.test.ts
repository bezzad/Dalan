// Rules: CLI-97
// Toasts do not pile up: the same text once, at most 3 at a time, so they never cover the menu for long.
import assert from "node:assert/strict";
import { test } from "node:test";
import { maxToasts, toastsToDrop } from "../src/toast.ts";

test("CLI-97 a toast with the same text replaces the one shown", () => {
  assert.deepEqual(toastsToDrop(["Failed"], "Failed"), [0]);
  assert.deepEqual(toastsToDrop(["Saved", "Failed"], "Failed"), [1]);
});

test("CLI-97 at most 3 toasts at once: a new one removes the oldest", () => {
  assert.equal(maxToasts, 3);
  assert.deepEqual(toastsToDrop(["a", "b"], "c"), []);
  assert.deepEqual(toastsToDrop(["a", "b", "c"], "d"), [0]);
  assert.deepEqual(toastsToDrop(["a", "b", "c", "d"], "e"), [0, 1]);
  // The same text goes first, so nothing else has to.
  assert.deepEqual(toastsToDrop(["a", "b", "c"], "b"), [1]);
});
