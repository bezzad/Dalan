// Rule: CLI-10
import assert from "node:assert/strict";
import { test } from "node:test";
import { leadingZeroBits, sha256, solve } from "../src/pow.ts";

test("CLI-10 the solved nonce gives a hash with the zero bits", async () => {
  const nonce = await solve("1900000000.c2FsdA.bWFj", 8);

  const hash = await sha256(`1900000000.c2FsdA.bWFj.${nonce}`);
  assert.equal(hash[0], 0);
  assert.match(nonce, /^[0-9]+$/);
});

test("CLI-10 zero bits are counted like on the server", async () => {
  // SHA-256("abc.0") = 20b699b1...: 0x20 is 0010 0000, so 2 zero bits (the server test SEC-06 uses the same text).
  assert.equal(leadingZeroBits(await sha256("abc.0")), 2);
  assert.equal(leadingZeroBits(new Uint8Array([0, 0, 1, 255])), 23);
  assert.equal(leadingZeroBits(new Uint8Array([0, 0])), 16);
  assert.equal(leadingZeroBits(new Uint8Array([255])), 0);
});
