// Rule: CLI-35
// The language button in the header: round, the size of the theme button, only a globe, one click switches.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

/** Just enough of the DOM for the header tools, run in Node. */
class FakeElement {
  readonly tag: string;
  readonly attributes = new Map<string, string>();
  readonly children: (FakeElement | string)[] = [];
  readonly listeners = new Map<string, () => void>();
  readonly classList = { add: (name: string) => this.setAttribute("class", `${this.className} ${name}`.trim()) };
  constructor(tag: string) { this.tag = tag; }
  get className(): string { return this.attributes.get("class") ?? ""; }
  set className(value: string) { this.setAttribute("class", value); }
  get title(): string { return this.attributes.get("title") ?? ""; }
  set title(value: string) { this.setAttribute("title", value); }
  set type(value: string) { this.setAttribute("type", value); }
  setAttribute(name: string, value: string): void { this.attributes.set(name, value); }
  getAttribute(name: string): string | null { return this.attributes.get(name) ?? null; }
  addEventListener(name: string, listener: () => void): void { this.listeners.set(name, listener); }
  append(...nodes: (FakeElement | string)[]): void { this.children.push(...nodes); }
  replaceChildren(...nodes: (FakeElement | string)[]): void { this.children.splice(0, this.children.length, ...nodes); }
  get textContent(): string { return this.children.map(child => typeof child === "string" ? child : child.textContent).join(""); }
}

Object.assign(globalThis, {
  document: { createElement: (tag: string) => new FakeElement(tag), createElementNS: (_: string, tag: string) => new FakeElement(tag) },
});

const { headerTools } = await import("../src/public.ts");
const { Language } = await import("../src/i18n.ts");
const { Theme } = await import("../src/theme.ts");

function storage() {
  const store = new Map<string, string>();
  return { getItem: (key: string) => store.get(key) ?? null, setItem: (key: string, value: string) => void store.set(key, value) };
}

test("CLI-35 the language button is round, shows only the globe, and one click switches the language", () => {
  const language = new Language({ lang: "", dir: "" }, storage());
  const theme = new Theme({ dataset: {} }, storage(), false);
  let clicks = 0;
  for (const [lang, other] of [["fa", "English"], ["en", "فارسی"]] as const) {
    assert.equal(language.current, lang);
    const tools = headerTools(language, theme, () => { clicks++; language.toggle(); }, document.createElement("span")) as unknown as FakeElement;
    const [button, themeButton] = tools.children as FakeElement[];
    assert.deepEqual(button!.className.split(" ").sort(), ["lang", "round"]);
    assert.equal(themeButton!.className, "theme");
    // Only the globe: one icon and no letters. Its name says the other language.
    assert.deepEqual(button!.children.map(child => (child as FakeElement).tag), ["svg"]);
    assert.equal(button!.textContent, "");
    assert.equal(button!.getAttribute("aria-label"), other);
    button!.listeners.get("click")!();
  }
  assert.equal(clicks, 2);
  assert.equal(language.current, "fa");
});

test("CLI-35 the language and theme buttons are both 36 by 36 pixels", () => {
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  const round = /button\.theme, button\.round \{([^}]*)\}/.exec(css)![1]!;
  for (const rule of ["width: 36px", "height: 36px", "padding: 0"]) assert.ok(round.includes(rule), `round buttons need "${rule}"`);
  assert.doesNotMatch(css, /button\.round\.lang \{/, "the language button has no size of its own");
});
