// Rules: CLI-26, CLI-27, CLI-28, CLI-29
import assert from "node:assert/strict";
import { test } from "node:test";
import { categoryName, faqItems, initial, landing, pageFor, privacyPoints, showcaseStateName, type PublicNumbers } from "../src/pages.ts";

const numbers: PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60 };
const persian = (value: number) => new Intl.NumberFormat("fa-IR").format(value);
const english = (value: number) => String(value);

test("CLI-26 the landing page has the three messages in order and never says tunnel", () => {
  assert.match(landing.en[0]!.title, /own computer/);
  assert.match(landing.en[1]!.title, /verified by mobile number/);
  assert.match(landing.en[2]!.title, /international internet is cut/);
  assert.equal(landing.fa.length, 3);
  for (const message of [...landing.fa, ...landing.en]) {
    assert.doesNotMatch(message.title + message.text, /tunnel|تونل/i);
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
