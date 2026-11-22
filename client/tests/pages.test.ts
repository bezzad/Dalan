// Rules: CLI-26, CLI-27, CLI-28, CLI-29, CLI-30, CLI-37
import assert from "node:assert/strict";
import { test } from "node:test";
import { categoryName, faqItems, initial, landing, notOffered, offers, products, security, pageFor, privacyPoints, readyPrompt, showcaseStateName, type PublicNumbers } from "../src/pages.ts";

const numbers: PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://dalanet.ir", bot_api_url: "https://api.dalanet.ir" };
const persian = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
const english = (value: number) => String(value);

test("CLI-26 the landing page has the three messages in order and never says tunnel", () => {
  assert.match(landing.en[0]!.title, /own computer/);
  assert.match(landing.en[1]!.title, /verified by email or SMS/);
  assert.match(landing.en[2]!.title, /outside links are down/);
  assert.match(landing.fa[2]!.title, /قطعی اینترنت بین‌الملل/);
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

test("CLI-26 two plans, both built, each opens its own guide", () => {
  for (const lang of ["fa", "en"] as const) {
    assert.deepEqual(products[lang].map(product => product.soon), [false, false]);
    assert.deepEqual(products[lang].map(product => product.guide), ["/guide/bot", "/guide/site"]);
    assert.ok(security[lang].length >= 6);
    assert.doesNotMatch(JSON.stringify([products[lang], security[lang]]), /tunnel|تونل/i);
  }
});

test("CLI-37 what you get: three ready groups, then sites and gateway: a local site is built, the rest soon", () => {
  for (const lang of ["fa", "en"] as const) {
    const groups = offers[lang];
    assert.equal(groups.length, 4);
    for (const group of groups.slice(0, 3)) {
      assert.ok(group.items.length >= 3, group.title);
      assert.ok(group.items.every(item => !item.soon), group.title);
    }
    assert.ok(!groups[3]!.items[0]!.soon);
    assert.ok(groups[3]!.items.slice(1).every(item => item.soon));
    assert.match(groups[0]!.items[0]!.text, /Bot API/);
  }
  assert.match(offers.en[3]!.title, /Sites/);
});

test("CLI-37 says what Dalan does not offer and never says tunnel", () => {
  for (const lang of ["fa", "en"] as const) {
    assert.match(notOffered[lang], /TCP/);
    assert.match(notOffered[lang], /SSH/);
    assert.match(notOffered[lang], /AI|هوش مصنوعی/);
    assert.doesNotMatch(JSON.stringify(offers[lang]) + notOffered[lang], /tunnel|تونل/i);
  }
});

test("CLI-26 the address and the login decide the screen", () => {
  const paths = ["/", "/bots", "/apps", "/privacy", "/faq", "/showcase", "/@bot"];
  assert.deepEqual(paths.map(path => pageFor(path, false)), ["landing", "login", "login", "privacy", "faq", "showcase", "login"]);
  assert.deepEqual(paths.map(path => pageFor(path, true)), ["landing", "bots", "apps", "privacy", "faq", "showcase", "chat"]);
});

test("CLI-40 an unknown address is not found, never the landing page", () => {
  for (const loggedIn of [false, true]) {
    assert.equal(pageFor("/no-such-page", loggedIn), "notFound");
    assert.equal(pageFor("/bots/7", loggedIn), "notFound");
    assert.equal(pageFor("/", loggedIn), "landing");
  }
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

