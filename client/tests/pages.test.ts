// Rules: CLI-26, CLI-27, CLI-28, CLI-29, CLI-30
import assert from "node:assert/strict";
import { test } from "node:test";
import { categoryName, faqItems, initial, installCommands, landing, problems, products, security, steps, pageFor, privacyPoints, readyPrompt, showcaseStateName, type PublicNumbers } from "../src/pages.ts";

const numbers: PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://dalanet.ir", bot_api_url: "https://api.dalanet.ir" };
const persian = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
const english = (value: number) => String(value);

test("CLI-26 the landing page has the three messages in order and never says tunnel", () => {
  assert.match(landing.en[0]!.title, /own computer/);
  assert.match(landing.en[1]!.title, /verified by email or SMS/);
  assert.match(landing.en[2]!.title, /international internet is cut/);
  assert.equal(landing.fa.length, 3);
  for (const message of [...landing.fa, ...landing.en]) {
    assert.doesNotMatch(message.title + message.text, /tunnel|تونل/i);
  }
});

test("CLI-26 the bot card shows users, Dalan and your computer, and the computer only calls out", () => {
  const [en, fa] = [products.en[0]!, products.fa[0]!];
  assert.match(en.from, /users/i);
  assert.match(en.frame, /your computer/i);
  assert.match(en.frame, /outgoing only/);
  assert.match(fa.frame, /خروجی/);
  // The traffic splits in two below Dalan, like two bots on one computer.
  for (const product of [...products.en, ...products.fa]) assert.equal(product.branches.length, 2);
});

test("CLI-26 the steps use the addresses from the public settings", () => {
  const values = { ...numbers, site_url: "https://example.ir", bot_api_url: "https://api.example.ir" };
  for (const lang of ["fa", "en"] as const) {
    const list = steps(lang, values);
    assert.equal(list.length, 3);
    assert.ok(list[1]!.code!.includes("https://api.example.ir/bot<token>/getUpdates"));
    assert.ok(list[2]!.code!.includes("https://example.ir/@"));
    for (const step of list) assert.doesNotMatch(step.title + step.text + (step.code ?? ""), /tunnel|تونل|dalanet/i);
  }
});

test("CLI-26 try it on every system with the site address from the settings", () => {
  const values = { ...numbers, site_url: "https://example.ir" };
  const tabs = installCommands(values);
  assert.deepEqual(tabs.map(tab => tab.os), ["Linux", "macOS", "Windows", "Docker"]);
  for (const tab of tabs) assert.ok(tab.code.includes("https://example.ir/dalan.mjs"), tab.os);
});

test("CLI-26 only real things are offered: what is not built says soon", () => {
  for (const lang of ["fa", "en"] as const) {
    const items = problems[lang].flatMap(group => group.items);
    assert.ok(items.some(item => item.soon) && items.some(item => !item.soon));
    for (const item of items) assert.ok(item.soon || item.href, item.title);
    assert.deepEqual(products[lang].map(product => product.soon), [false, true]);
    assert.ok(security[lang].length >= 6);
    const all = JSON.stringify([problems[lang], products[lang], security[lang]]);
    assert.doesNotMatch(all, /tunnel|تونل/i);
  }
});

test("CLI-26 the address and the login decide the screen", () => {
  const paths = ["/", "/privacy", "/faq", "/showcase", "/@bot"];
  assert.deepEqual(paths.map(path => pageFor(path, false)), ["landing", "privacy", "faq", "showcase", "login"]);
  assert.deepEqual(paths.map(path => pageFor(path, true)), ["bots", "privacy", "faq", "showcase", "chat"]);
});

test("CLI-27 privacy page and FAQ say six things with the numbers from the server", () => {
  for (const [lang, n] of [["fa", persian], ["en", english]] as const) {
    const points = privacyPoints(lang, numbers, n);
    const faq = faqItems(lang, numbers, n);
    assert.equal(points.length, 6);
    assert.equal(faq.length, 6);
    for (const text of [points.join(" "), faq.map(item => item.answer).join(" ")]) {
      for (const value of [200, 30, 180]) assert.ok(text.includes(n(value)), `${lang} has ${value}`);
    }
    assert.ok(faq[4]!.answer.includes(n(60)));
  }
  assert.ok(privacyPoints("fa", numbers, persian).join(" ").includes("۱۸۰"));
});

test("CLI-27 the numbers are not in the text: other numbers give other words", () => {
  const other = privacyPoints("en", { ...numbers, access_log_days: 365 }, english).join(" ");
  assert.ok(other.includes("365"));
  assert.ok(!other.includes("180"));
});

test("CLI-28 start categories have names, others are shown as they are", () => {
  assert.equal(categoryName("en", "games"), "Games");
  assert.equal(categoryName("fa", "games"), "بازی");
  assert.equal(categoryName("en", "robots"), "robots");
  assert.equal(initial(" weather"), "W");
  assert.equal(initial("هوا"), "ه");
});

test("CLI-29 the state of each entry has a name", () => {
  const entry = (state: "pending" | "listed" | "refused") => ({ state, description: "", category: "games", icon: false });
  assert.deepEqual(
    [showcaseStateName("en", null), showcaseStateName("en", entry("pending")), showcaseStateName("en", entry("listed")), showcaseStateName("en", entry("refused"))],
    ["Not listed", "Waiting for review", "Listed", "Refused"]);
  assert.equal(showcaseStateName("fa", null), "در ویترین نیست");
});

test("CLI-30 the ready prompt names the addresses from the server and no key", () => {
  for (const lang of ["fa", "en"] as const) {
    const text = readyPrompt(lang, numbers.site_url, numbers.bot_api_url);
    for (const address of ["https://dalanet.ir/llms.txt", "https://dalanet.ir/api/mcp", "https://dalanet.ir/dalan.mjs", "https://api.dalanet.ir"]) {
      assert.ok(text.includes(address), `${lang} has ${address}`);
    }
    assert.doesNotMatch(text, /dalan_[A-Za-z0-9]/);
  }
  assert.match(readyPrompt("en", "https://other.ir", "https://bots.other.ir"), /https:\/\/bots\.other\.ir/);
});

