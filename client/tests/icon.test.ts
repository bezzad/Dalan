// Rule: CLI-50
// The square cut and the size fit of a bot icon, and its address.
import assert from "node:assert/strict";
import { test } from "node:test";
import { botIconUrl, firstThatFits, qualities, squareCut } from "../src/icon.ts";

test("CLI-50 a wide or tall picture keeps its middle square", () => {
  assert.deepEqual(squareCut(1200, 800), { x: 200, y: 0, size: 800 });
  assert.deepEqual(squareCut(600, 900), { x: 0, y: 150, size: 600 });
  assert.deepEqual(squareCut(512, 512), { x: 0, y: 0, size: 512 });
});

test("CLI-50 the quality falls until the icon fits the limit", async () => {
  const tried: number[] = [];
  // A fake encoder: better quality, bigger file.
  const encode = async (quality: number) => {
    tried.push(quality);
    return new Blob([new Uint8Array(Math.round(quality * 200 * 1024))]);
  };
  const fit = await firstThatFits(encode, 100 * 1024);
  assert.ok(fit && fit.size <= 100 * 1024);
  assert.deepEqual(tried, [0.9, 0.8, 0.7, 0.6, 0.5]);
  assert.equal(await firstThatFits(encode, 10), null);
  assert.equal(tried.length, 5 + qualities.length);
});

test("CLI-50 no icon asks for no image; a new version is a new address", () => {
  assert.equal(botIconUrl(4, 0), null);
  assert.equal(botIconUrl(4, undefined), null);
  assert.equal(botIconUrl(4, 3), "/api/bots/4/icon?v=3");
});
