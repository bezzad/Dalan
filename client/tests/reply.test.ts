// Rules: CLI-13, CLI-52, CLI-53, CLI-54, CLI-55
import assert from "node:assert/strict";
import { test } from "node:test";
import { formatNodes, formatTree, type Part } from "../src/format.ts";
import { buttonAction, fillText, appLinksFor, isReplySwipe, PressState, quoteLine, type Message } from "../src/messages.ts";

const m = (id: number, version = id): Message => ({ id, version, from: "bot", text: `text ${id}`, date: "2026-11-24T12:00:00Z" });

test("CLI-13: a switch button fills the input with @username and the query", () => {
  assert.deepEqual(buttonAction({ text: "Edit", switch_query: "todo 7" }), { fill: "todo 7" });
  assert.deepEqual(buttonAction({ text: "Empty", switch_query: "" }), { fill: "" });
  assert.equal(fillText("mofidbot", "todo 7"), "@mofidbot todo 7");
  assert.equal(fillText("mofidbot", "\nBuy milk"), "@mofidbot \nBuy milk");
});

test("CLI-52: the quote shows the first line, or the file name", () => {
  assert.equal(quoteLine({ id: 2, from: "bot", text: "line one\nline two" }), "line one");
  assert.equal(quoteLine({ id: 2, from: "bot", text: "\n  \nsecond" }), "second");
  assert.equal(quoteLine({ id: 2, from: "user", text: "", file: { id: "f", kind: "photo", name: "cat.jpg", type: "image/jpeg", size: 1 } }), "cat.jpg");
});

test("CLI-52: a swipe to the left starts a reply; a scroll does not", () => {
  assert.equal(isReplySwipe(-80, 10), true);
  assert.equal(isReplySwipe(-40, 0), false);
  assert.equal(isReplySwipe(80, 0), false);
  assert.equal(isReplySwipe(-80, 70), false);
});

test("CLI-53: a press waits until the bot answers it", () => {
  const presses = new PressState();
  const message = m(2);
  const token = presses.start(message, 0, 1000, 15);
  assert.equal(presses.sent(2, token, "77"), false);

  assert.equal(presses.pending(message, 2000), 0);
  assert.equal(presses.answered("78"), false);
  assert.equal(presses.answered("77"), true);
  assert.equal(presses.pending(message, 2000), null);
});

test("CLI-53: a newer copy of the message, or the end of the time, ends the wait", () => {
  const presses = new PressState();
  presses.start(m(2), 1, 1000, 15);
  assert.equal(presses.pending(m(2, 5), 2000), null);

  presses.start(m(3), 0, 1000, 15);
  assert.equal(presses.pending(m(3), 15_999), 0);
  assert.equal(presses.pending(m(3), 16_000), null);
});

test("CLI-53: an answer that comes before the press id still ends the wait; a failure ends it too", () => {
  const presses = new PressState();
  const token = presses.start(m(2), 0, 1000, 15);
  presses.answered("77");
  assert.equal(presses.sent(2, token, "77"), true);
  assert.equal(presses.pending(m(2), 2000), null);

  const failed = presses.start(m(4), 0, 1000, 15);
  presses.end(4, failed);
  assert.equal(presses.pending(m(4), 2000), null);
});

test("CLI-53: a late answer or failure of an older press never ends a newer press of the same message", () => {
  const presses = new PressState();
  const old = presses.start(m(2), 0, 1000, 15);
  assert.equal(presses.pending(m(2), 17_000), null);
  presses.start(m(2), 1, 17_000, 15);

  assert.equal(presses.sent(2, old, "77"), false);
  presses.end(2, old);
  assert.equal(presses.answered("77"), false);
  assert.equal(presses.pending(m(2), 18_000), 1);
});

test("CLI-55: a bot's chat links that bot's manifest and icons; other screens Dalan's", () => {
  assert.deepEqual(appLinksFor("/@mofidbot"), {
    manifest: "/@mofidbot/manifest.webmanifest", icon: "/@mofidbot/icon", iconType: null, appleIcon: "/@mofidbot/icon",
  });
  assert.deepEqual(appLinksFor("/chats"), {
    manifest: "/manifest.webmanifest", icon: "/icons/icon.svg", iconType: "image/svg+xml", appleIcon: "/icons/icon-192.png",
  });
  assert.equal(appLinksFor("/@bad\"name").manifest, "/manifest.webmanifest");
});

/** The tree as short text: [type: parts]. */
const show = (parts: Part[]): string =>
  parts.map(part => typeof part === "string" ? part : `[${part.entity.type}: ${show(part.parts)}]`).join("");

test("CLI-54: bold part and plain text", () => {
  assert.equal(show(formatTree("hi Ali", [{ type: "bold", offset: 3, length: 3 }])), "hi [bold: Ali]");
});

test("CLI-54: nested entities nest; broken ones are dropped and the text stays", () => {
  assert.equal(show(formatTree("abc", [{ type: "italic", offset: 1, length: 1 }, { type: "bold", offset: 0, length: 3 }])), "[bold: a[italic: b]c]");
  assert.equal(show(formatTree("abcd", [{ type: "bold", offset: 0, length: 3 }, { type: "italic", offset: 2, length: 2 }])), "[bold: abc]d");
  assert.equal(show(formatTree("ab", [{ type: "bold", offset: 1, length: 5 }])), "ab");
});

test("CLI-54: offsets count UTF-16 units, as the server does", () => {
  assert.equal(show(formatTree("😀 سلام", [{ type: "bold", offset: 3, length: 4 }])), "😀 [bold: سلام]");
});

/** Just enough of the DOM to draw formatted text in Node. */
class FakeNode {
  readonly tag: string;
  readonly text: string;
  readonly attributes = new Map<string, string>();
  readonly children: FakeNode[] = [];
  className = "";
  readonly classList = { add: (name: string) => { this.className = `${this.className} ${name}`.trim(); } };
  constructor(tag: string, text = "") { this.tag = tag; this.text = text; }
  append(...nodes: FakeNode[]): void { this.children.push(...nodes); }
  setAttribute(name: string, value: string): void { this.attributes.set(name, value); }
  addEventListener(): void {}
}
const all = (nodes: FakeNode[]): FakeNode[] => nodes.flatMap(node => [node, ...all(node.children)]);

test("CLI-54: markup in the text stays text", () => {
  Object.assign(globalThis, {
    document: { createElement: (tag: string) => new FakeNode(tag), createTextNode: (text: string) => new FakeNode("#text", text) },
  });
  const text = "<img src=x onerror=alert(1)>";
  const nodes = formatNodes(formatTree(text, [{ type: "bold", offset: 0, length: text.length }])) as unknown as FakeNode[];

  const bold = nodes[0]!;
  assert.equal(bold.tag, "strong");
  assert.deepEqual(bold.children.map(child => [child.tag, child.text]), [["#text", text]]);
  assert.equal(all(nodes).some(node => node.tag === "img"), false);
});

test("CLI-54: a link opens in a new tab without referrer; a link that is not http(s) gets no address", () => {
  Object.assign(globalThis, {
    document: { createElement: (tag: string) => new FakeNode(tag), createTextNode: (text: string) => new FakeNode("#text", text) },
  });
  const [good, bad] = formatNodes(formatTree("ab", [
    { type: "text_link", offset: 0, length: 1, url: "https://dalanet.ir" },
    { type: "text_link", offset: 1, length: 1, url: "javascript:alert(1)" },
  ])) as unknown as FakeNode[];

  assert.equal(good!.attributes.get("href"), "https://dalanet.ir");
  assert.equal(good!.attributes.get("rel"), "noopener noreferrer");
  assert.equal(good!.attributes.get("target"), "_blank");
  assert.equal(bad!.attributes.has("href"), false);
});
