// Rules: CLI-45, CLI-80, CLI-91
// The chat page is the bot's own page: its icon, name and status, then search, theme, the user menu and the Dalan mark.
import assert from "node:assert/strict";
import { test } from "node:test";
import type { Message } from "../src/messages.ts";

/** Just enough of the DOM for the chat header, run in Node. */
class FakeElement {
  readonly tag: string;
  readonly attributes = new Map<string, string>();
  readonly children: (FakeElement | string)[] = [];
  readonly listeners = new Map<string, (event?: unknown) => void>();
  value = "";
  readonly classList = { add: (name: string) => this.setAttribute("class", `${this.className} ${name}`.trim()) };
  constructor(tag: string) { this.tag = tag; }
  get className(): string { return this.attributes.get("class") ?? ""; }
  get hidden(): boolean { return this.attributes.has("hidden"); }
  set hidden(value: boolean) { if (value) this.attributes.set("hidden", ""); else this.attributes.delete("hidden"); }
  set textContent(value: string) { this.children.splice(0, this.children.length, value); }
  get textContent(): string { return this.children.map(child => typeof child === "string" ? child : child.textContent).join(""); }
  setAttribute(name: string, value: string): void { this.attributes.set(name, value); }
  getAttribute(name: string): string | null { return this.attributes.get(name) ?? null; }
  addEventListener(name: string, listener: (event?: unknown) => void): void { this.listeners.set(name, listener); }
  append(...nodes: (FakeElement | string)[]): void { this.children.push(...nodes); }
  replaceChildren(...nodes: (FakeElement | string)[]): void { this.children.splice(0, this.children.length, ...nodes); }
  focus(): void {}
}
const all = (root: FakeElement): FakeElement[] => root.children.flatMap(child => typeof child === "string" ? [] : [child, ...all(child)]);
Object.assign(globalThis, {
  document: {
    createElement: (tag: string) => new FakeElement(tag),
    createElementNS: (_: string, tag: string) => new FakeElement(tag),
  },
});

const { chatHeader, matchingMessages, micButton, offlineBar } = await import("../src/chatHeader.ts");

const t = (key: "search" | "close") => ({ search: "Search", close: "Close" })[key];
const tool = (name: string) => { const element = new FakeElement("button"); element.setAttribute("class", name); return element as unknown as HTMLElement; };

test("CLI-45 the chat header shows the bot, then search, theme, the user menu and the Dalan mark", () => {
  const header = chatHeader({ username: "mofid", t, onSearch: () => {}, tools: [tool("theme"), tool("menu user"), tool("home")] });
  header.showBot("Arcane", null);
  const element = header.element as unknown as FakeElement;
  const text = element.textContent;

  assert.ok(text.includes("A") && text.includes("Arcane") && text.includes("@mofid"), text);
  const buttons = all(element).filter(child => child.tag === "button").map(child => child.className);
  assert.deepEqual(buttons.filter(name => name !== "round close"), ["round search", "theme", "menu user", "home"]);
  assert.doesNotMatch(text, /Back|بازگشت/);
  assert.ok(!all(element).some(child => child.className.includes("lang")), "no language button");
});

test("CLI-45 search opens a box, sends the words, and Escape closes it with an empty search", () => {
  const words: string[] = [];
  const header = chatHeader({ username: "mofid", t, onSearch: query => words.push(query), tools: [] });
  const element = header.element as unknown as FakeElement;
  const box = all(element).find(child => child.className === "search")!;
  const input = all(box).find(child => child.tag === "input")!;
  assert.ok(box.hidden);

  all(element).find(child => child.className === "round search")!.listeners.get("click")!();
  assert.ok(!box.hidden);
  input.value = "clock";
  input.listeners.get("input")!();
  input.listeners.get("keydown")!({ key: "Escape" });

  assert.ok(box.hidden);
  assert.deepEqual(words, ["clock", ""]);
});

test("CLI-45 search keeps only the messages whose text or file name has the words", () => {
  const message = (id: number, text: string, file?: string): Message => ({ id, version: id, from: "bot", text, date: "2026-11-23T10:00:00Z",
    ...(file ? { file: { id: "f", kind: "photo" as const, name: file, type: "image/png", size: 1 } } : {}) });
  const messages = [message(1, "Hello"), message(2, "the clock"), message(3, "", "Clock.png")];

  assert.deepEqual(matchingMessages(messages, " CLOCK ").map(m => m.id), [2, 3]);
  assert.deepEqual(matchingMessages(messages, "").map(m => m.id), [1, 2, 3]);
});

test("CLI-80 the voice button shows the line mic svg, no emoji, and keeps the name Record", () => {
  const button = micButton("Record") as unknown as FakeElement;
  assert.equal(button.getAttribute("aria-label"), "Record");
  const svg = button.children[0] as FakeElement;
  assert.equal(svg.tag, "svg");
  assert.ok(svg.className.includes("icon"));
  const paths = svg.children.map(path => (path as FakeElement).getAttribute("d"));
  assert.deepEqual(paths, ["M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3z", "M19 10v2a7 7 0 0 1-14 0v-2", "M12 19v3"]);
  assert.equal(button.textContent, "");
});

const offlineText = "{name} is offline and won't answer now. Your messages wait for it; if it doesn't come back in time, they show \"not delivered\".";

test("CLI-91 the offline bar shows for an offline or never-seen bot and names it", () => {
  const bar = offlineBar("mofid", offlineText);
  const element = bar.element as unknown as FakeElement;
  assert.ok(element.hidden, "hidden before the first status");

  bar.update({ online: false, last_seen: "2026-11-26T10:00:00Z" }, false);
  assert.ok(!element.hidden);
  assert.match(element.textContent, /^@mofid is offline and won't answer now\./);
  assert.match(element.textContent, /not delivered/);
  assert.equal(element.getAttribute("role"), "status");

  bar.update({ online: false, last_seen: null }, false);
  assert.ok(!element.hidden, "a bot never seen is offline too");
});

test("CLI-91 the offline bar hides when the bot comes back or is suspended", () => {
  const bar = offlineBar("mofid", offlineText);
  const element = bar.element as unknown as FakeElement;
  bar.update({ online: false, last_seen: null }, false);

  bar.update({ online: true, last_seen: "2026-11-26T10:00:00Z" }, false);
  assert.ok(element.hidden);

  bar.update({ online: false, last_seen: null }, true);
  assert.ok(element.hidden, "a suspended bot shows its own error instead");
});
