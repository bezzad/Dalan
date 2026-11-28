// Rules: CLI-35, CLI-42, CLI-26, CLI-56, CLI-57, CLI-104
// The header (language and theme buttons, three links, the ☰ menu), the footer links, and a landing page with no install steps.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

/** Just enough of the DOM for the header and the landing page, run in Node. */
class FakeElement {
  readonly tag: string;
  readonly attributes = new Map<string, string>();
  readonly children: (FakeElement | string)[] = [];
  readonly listeners = new Map<string, (event?: unknown) => void>();
  readonly classList = { add: (name: string) => this.setAttribute("class", `${this.className} ${name}`.trim()) };
  constructor(tag: string) { this.tag = tag; }
  get className(): string { return this.attributes.get("class") ?? ""; }
  set className(value: string) { this.setAttribute("class", value); }
  get title(): string { return this.attributes.get("title") ?? ""; }
  set title(value: string) { this.setAttribute("title", value); }
  set type(value: string) { this.setAttribute("type", value); }
  get hidden(): boolean { return this.attributes.has("hidden"); }
  set hidden(value: boolean) { if (value) this.attributes.set("hidden", ""); else this.attributes.delete("hidden"); }
  set textContent(value: string) { this.children.splice(0, this.children.length, value); }
  get textContent(): string { return this.children.map(child => typeof child === "string" ? child : child.textContent).join(""); }
  setAttribute(name: string, value: string): void { this.attributes.set(name, value); }
  getAttribute(name: string): string | null { return this.attributes.get(name) ?? null; }
  addEventListener(name: string, listener: (event?: unknown) => void): void { this.listeners.set(name, listener); }
  append(...nodes: (FakeElement | string)[]): void { this.children.push(...nodes); }
  prepend(...nodes: (FakeElement | string)[]): void { this.children.unshift(...nodes); }
  replaceChildren(...nodes: (FakeElement | string)[]): void { this.children.splice(0, this.children.length, ...nodes); }
  contains(other: unknown): boolean { return other === this || all(this).includes(other as FakeElement); }
}

/** Every element under `root`, depth first. */
function all(root: FakeElement): FakeElement[] {
  return root.children.flatMap(child => typeof child === "string" ? [] : [child, ...all(child)]);
}

const documentListeners = new Map<string, (event: unknown) => void>();
Object.assign(globalThis, {
  document: {
    createElement: (tag: string) => new FakeElement(tag),
    createElementNS: (_: string, tag: string) => new FakeElement(tag),
    addEventListener: (name: string, listener: (event: unknown) => void) => documentListeners.set(name, listener),
    removeEventListener: (name: string) => documentListeners.delete(name),
  },
});

const { headerTools, publicScreen } = await import("../src/public.ts");
const { Language } = await import("../src/i18n.ts");
const { Theme } = await import("../src/theme.ts");

function storage() {
  const store = new Map<string, string>();
  return { getItem: (key: string) => store.get(key) ?? null, setItem: (key: string, value: string) => void store.set(key, value) };
}

function screen(page: "landing" | "about" | "contact", lang: "fa" | "en", email: string | null = null): FakeElement {
  const language = new Language({ lang: "", dir: "" }, storage());
  if (lang === "en") language.toggle();
  return publicScreen(page, {
    language, loggedIn: email !== null, email, user: null, go: () => {}, enter: () => {}, tools: document.createElement("div") }) as unknown as FakeElement;
}

const landing = (lang: "fa" | "en") => screen("landing", lang);

const byTag = (root: FakeElement, tag: string) => all(root).filter(element => element.tag === tag);
const hasClass = (element: FakeElement, name: string) => element.className.split(" ").includes(name);
const hrefs = (root: FakeElement) => byTag(root, "a").map(link => link.getAttribute("href"));

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

test("CLI-42 the header links to plans, showcase and guide; FAQ and privacy are only in the footer", () => {
  for (const lang of ["fa", "en"] as const) {
    const page = landing(lang);
    const header = byTag(page, "header")[0]!;
    const footer = byTag(page, "footer")[0]!;
    const nav = all(header).find(element => element.tag === "nav")!;
    assert.deepEqual(hrefs(nav), ["/#plans", "/showcase", "/guide"]);
    assert.ok(!hrefs(header).some(href => href === "/faq" || href === "/privacy"), "no FAQ or privacy in the header");
    for (const href of ["/faq", "/privacy", "/guide"]) assert.ok(hrefs(footer).includes(href), `footer has ${href}`);
  }
});

test("CLI-42 the ☰ button opens and closes the phone menu", () => {
  const header = byTag(landing("en"), "header")[0]!;
  const button = all(header).find(element => hasClass(element, "burger"))!;
  const sheet = all(header).find(element => hasClass(element, "sheet"))!;
  assert.equal(button.getAttribute("aria-expanded"), "false");
  assert.ok(sheet.hidden);
  button.listeners.get("click")!({ stopPropagation() {} });
  assert.equal(button.getAttribute("aria-expanded"), "true");
  assert.ok(!sheet.hidden);
  assert.deepEqual(hrefs(sheet), ["/#plans", "/showcase", "/guide"]);
  button.listeners.get("click")!({ stopPropagation() {} });
  assert.equal(button.getAttribute("aria-expanded"), "false");
  assert.ok(sheet.hidden);
  // Escape closes it too.
  button.listeners.get("click")!({ stopPropagation() {} });
  documentListeners.get("keydown")!({ key: "Escape" });
  assert.ok(sheet.hidden);
});

test("CLI-42 on a phone the links fold into the ☰ button; list icons sit on the first line", () => {
  const css = readFileSync(new URL("../public/app.css", import.meta.url), "utf8");
  const phone = /@media \(max-width: 759px\) \{([^@]*)\}/.exec(css)?.[1] ?? "";
  assert.match(phone, /header\.site nav\.links \{[^}]*display: none/);
  assert.match(phone, /button\.burger \{[^}]*display: inline-flex/);
  assert.match(css, /button\.burger \{[^}]*display: none/);
  // Every icon list centres its icon on the first text line, for Inter and for Vazirmatn alike.
  assert.match(css, /ul\.ticks li \.icon \{[^}]*margin-block-start: calc\(\(1lh - /);
  assert.doesNotMatch(css, /li \.icon \{[^}]*margin-block-start: 2px/);
});

test("CLI-26 the landing page has the plans with their guides and no install steps", () => {
  for (const lang of ["fa", "en"] as const) {
    const page = landing(lang);
    const content = all(page).find(element => hasClass(element, "content"))!;
    for (const kind of ["code", "os-tabs", "steps", "try"]) {
      assert.ok(!all(content).some(element => hasClass(element, kind)), `no ${kind} on the landing page`);
    }
    assert.ok(!byTag(content, "pre").length, "no code");
    const plans = all(content).find(element => element.getAttribute("id") === "plans")!;
    assert.deepEqual(hrefs(plans), ["/guide/bot", "/guide/site"]);
    // Every list of the landing page with icons uses the shared ticks class.
    for (const list of byTag(content, "ul").filter(list => byTag(list, "svg").length)) assert.ok(hasClass(list, "ticks"), list.className);
  }
});

test("CLI-26 the hero has one call, and the strip of names follows it", () => {
  for (const lang of ["fa", "en"] as const) {
    const content = all(landing(lang)).find(element => hasClass(element, "content"))!;
    const sections = content.children as FakeElement[];
    const hero = sections[0]!;
    assert.ok(hasClass(hero, "hero"));
    assert.equal(byTag(hero, "button").length + byTag(hero, "a").length, 1, `${lang}: one call in the hero`);
    assert.ok(hasClass(sections[1]!, "strip"), `${lang}: the strip follows the hero`);
    // The names are Latin words, left to right in both languages; the second copy (for the loop) is hidden from readers.
    const lists = byTag(sections[1]!, "ul");
    assert.equal(lists.length, 2);
    assert.ok(byTag(lists[0]!, "li").length >= 6);
    assert.equal(lists[1]!.getAttribute("aria-hidden"), "true");
  }
});

test("CLI-104 each plan and each 'What you get' card has its own colour", () => {
  for (const lang of ["fa", "en"] as const) {
    const content = all(landing(lang)).find(element => hasClass(element, "content"))!;
    const tones = (id: string) => byTag(all(content).find(element => element.getAttribute("id") === id)!, "article")
      .map(card => card.className.split(" ").find(name => name.startsWith("tone-")));
    for (const [id, count] of [["plans", 2], ["offers", 4]] as const) {
      const found = tones(id);
      assert.equal(found.length, count, id);
      assert.ok(found.every(Boolean), `${id}: every card has a tone`);
      assert.equal(new Set(found).size, count, `${id}: all different`);
    }
  }
});

test("CLI-56 every public page links about and contact in the footer", () => {
  for (const lang of ["fa", "en"] as const) {
    for (const page of ["landing", "about", "contact"] as const) {
      const footer = byTag(screen(page, lang), "footer")[0]!;
      for (const href of ["/about", "/contact"]) assert.ok(hrefs(footer).includes(href), `${page} ${lang} footer has ${href}`);
    }
  }
});

test("CLI-56 the about page shows only the current language", () => {
  const persianLetters = /[\u0600-\u06FF]/;
  const fa = all(screen("about", "fa")).find(element => hasClass(element, "content"))!.textContent;
  const en = all(screen("about", "en")).find(element => hasClass(element, "content"))!.textContent;
  assert.match(fa, /دالان چیست؟/);
  assert.doesNotMatch(fa, /What is Dalan/);
  assert.match(en, /What is Dalan\?/);
  assert.doesNotMatch(en, persianLetters);
});

test("CLI-57 a visitor types the email; a user with an email gets it filled and read only", () => {
  const email = (page: FakeElement) => all(page).find(element => element.tag === "input" && element.getAttribute("type") === "email")!;
  const visitor = email(screen("contact", "en"));
  assert.ok(visitor.attributes.has("required"));
  assert.ok(!visitor.attributes.has("readonly"));
  assert.equal(visitor.getAttribute("value"), "");
  const sara = email(screen("contact", "fa", "sara@example.com"));
  assert.ok(sara.attributes.has("readonly"));
  assert.equal(sara.getAttribute("value"), "sara@example.com");
  const page = screen("contact", "en");
  assert.equal(byTag(page, "textarea").length, 1);
  assert.ok(byTag(page, "button").some(button => button.getAttribute("type") === "submit"));
});
