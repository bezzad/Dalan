// Rules: CLI-75
// The connection ring: down and up set and clear the ring on the header icon, the tooltip and the status text.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

class FakeElement {
  className = "";
  readonly attributes = new Map<string, string>();
  textContent = "";
  readonly classes = new Set<string>();
  readonly classList = { add: (name: string) => this.classes.add(name), remove: (name: string) => this.classes.delete(name) };
  setAttribute(name: string, value: string): void { this.attributes.set(name, value); }
  getAttribute(name: string): string | null { return this.attributes.get(name) ?? null; }
  removeAttribute(name: string): void { this.attributes.delete(name); }
}
Object.assign(globalThis, { document: { createElement: () => new FakeElement() } });

const { Connection } = await import("../src/connection.ts");
const texts = { connected: "Connected", connecting: "Connecting…" };

function setup() {
  const icon = new FakeElement();
  const logo = new FakeElement();
  let inChat = true;
  const connection = new Connection(() => (inChat ? icon : logo) as unknown as HTMLElement, () => texts);
  return { icon, logo, connection, status: connection.status as unknown as FakeElement, leaveChat: () => { inChat = false; } };
}

test("CLI-75 stream down turns a ring on the bot icon, back clears it", () => {
  const { icon, connection, status } = setup();
  connection.setUp();
  assert.equal(icon.classes.has("ring"), false);
  assert.equal(icon.getAttribute("title"), "Connected");
  assert.equal(status.textContent, "Connected");

  connection.setDown();
  assert.equal(icon.classes.has("ring"), true);
  assert.equal(icon.getAttribute("title"), "Connecting…");
  assert.equal(status.textContent, "Connecting…");
  assert.equal(status.getAttribute("role"), "status");

  connection.setUp();
  assert.equal(icon.classes.has("ring"), false);
  assert.equal(icon.getAttribute("title"), "Connected");
  assert.equal(status.textContent, "Connected");
});

test("CLI-75 on a screen with no bot icon the ring turns around the logo, also after a screen change", () => {
  const { icon, logo, connection, leaveChat } = setup();
  connection.setDown();
  leaveChat();
  connection.apply();
  assert.equal(icon.classes.has("ring"), false);
  assert.equal(logo.classes.has("ring"), true);
});

test("CLI-75 with no stream (login, public pages) there is no ring and no tooltip", () => {
  const { icon, connection, status } = setup();
  connection.apply();
  assert.equal(icon.getAttribute("title"), null);
  connection.setDown();
  connection.reset();
  assert.equal(icon.classes.has("ring"), false);
  assert.equal(icon.getAttribute("title"), null);
  assert.equal(status.textContent, "");
});

test("CLI-75 the style sheet has no Connecting bar; the ring is still and dashed with reduced motion", () => {
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  assert.doesNotMatch(css, /body\.offline/);
  assert.match(css, /\.ring::after/);
  assert.match(css, /prefers-reduced-motion: reduce\) \{\s*\.ring::after[^}]*dashed/);
  const main = readFileSync(new URL("../src/main.ts", import.meta.url), "utf8");
  assert.doesNotMatch(main, /offline|data-connecting|dataset\["connecting"\]/);
});

test("CLI-75 the ring is a true circle on the icon's centre, also around the square logo (owner, 2026-11-27)", () => {
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  const ring = /\.ring::after \{([^}]*)\}/.exec(css)?.[1] ?? "";
  assert.match(ring, /aspect-ratio: 1;/);
  assert.match(ring, /border-radius: 50%;/);
  assert.match(ring, /top: 50%;[^]*left: 50%;[^]*translate: -50% -50%;/);
  // A turning rounded square looks like a bent line that wobbles: the logo gets no shape of its own.
  assert.doesNotMatch(css, /\.mark\.ring::after \{/);
});
