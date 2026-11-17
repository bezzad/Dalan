// Rule: CLI-12
import assert from "node:assert/strict";
import { test } from "node:test";
import { latinDigits } from "../src/digits.ts";

test("CLI-12 a phone number in Persian digits becomes Latin digits", () => {
  assert.equal(latinDigits("۰۹۱۲۱۲۳۴۵۶۷"), "09121234567");
});

test("CLI-12 a code in Arabic digits becomes Latin digits", () => {
  assert.equal(latinDigits("١٢٣٤٥٦"), "123456");
});

test("CLI-12 Latin digits, other letters and inner text stay; outer spaces go", () => {
  assert.equal(latinDigits(" +98 912 ۱۲۳ "), "+98 912 123");
});
