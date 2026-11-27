// Rules: CLI-89, CLI-90
// The picture viewer's zoom, and the HTML viewer's frame rules. The viewers themselves run in a real browser (tests/client-e2e).
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { clampPan, doubleTap, pinch, zoomAt, type View } from "../src/zoom.ts";

const box = { width: 400, height: 300 };
const start: View = { scale: 1, x: 0, y: 0 };

test("CLI-89 a double tap zooms in at that point, the next one goes back to full size", () => {
  const zoomed = doubleTap(start, { x: 100, y: 100 }, box);
  assert.equal(zoomed.scale, 2.5);
  // The tapped point stays under the finger.
  assert.equal(100 - zoomed.x, 100 * 2.5);
  assert.equal(100 - zoomed.y, 100 * 2.5);
  assert.deepEqual(doubleTap(zoomed, { x: 10, y: 10 }, box), start);
});

test("CLI-89 a pinch scales between 1 and 4", () => {
  assert.equal(pinch(start, 100, 200, { x: 0, y: 0 }, box).scale, 2);
  assert.equal(pinch(start, 100, 1000, { x: 0, y: 0 }, box).scale, 4);
  assert.equal(pinch({ scale: 2, x: 0, y: 0 }, 100, 10, { x: 0, y: 0 }, box).scale, 1);
});

test("CLI-89 a pan stays inside the picture's edges", () => {
  const view = zoomAt(start, 2, { x: 0, y: 0 });
  assert.deepEqual(clampPan({ ...view, x: 50, y: 50 }, box), { scale: 2, x: 0, y: 0 });
  assert.deepEqual(clampPan({ ...view, x: -1000, y: -1000 }, box), { scale: 2, x: -400, y: -300 });
  assert.deepEqual(clampPan({ scale: 1, x: -20, y: 30 }, box), start, "at full size there is nothing to pan");
});

test("CLI-90 the frame is sandboxed with exactly allow-scripts allow-popups, never allow-same-origin", () => {
  const source = readFileSync(new URL("../src/viewer.ts", import.meta.url), "utf8");
  assert.match(source, /sandbox: "allow-scripts allow-popups"/);
  assert.doesNotMatch(source, /allow-same-origin/);
  assert.match(source, /referrerpolicy: "no-referrer"/);
  assert.match(source, /"noopener,noreferrer"/);
});
