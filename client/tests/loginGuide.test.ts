// Rules: CLI-97, AUTH-41, AUTH-43
import assert from "node:assert/strict";
import { test } from "node:test";
import { authorizeQuery, consentItems, discoveryUrl, hostOf, loginAddresses, loginGuideSteps, loginSamples, proxySamples } from "../src/loginGuide.ts";
import { loginTarget, safeNext } from "../src/next.ts";
import { pageFor, type PublicNumbers } from "../src/pages.ts";

const numbers: PublicNumbers = { history_messages: 200, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60,
  site_url: "https://example.ir", bot_api_url: "https://api.example.ir", contact_max_length: 2000, sites_domain: "sites.example.ir" };

test("CLI-97 /guide/login is a public page, in both languages, with both ways and this server's addresses", () => {
  assert.equal(pageFor("/guide/login", false), "guideLogin");
  assert.equal(pageFor("/guide/login", true), "guideLogin");
  for (const lang of ["fa", "en"] as const) {
    const steps = loginGuideSteps(lang, numbers);
    const all = JSON.stringify(steps);
    assert.ok(all.includes("dalan http 3000 --name shop --login"), `${lang}: way 1`);
    assert.ok(all.includes("X-Dalan-User") && all.includes("X-Dalan-Name"), `${lang}: the headers`);
    assert.ok(all.includes("https://example.ir/.well-known/openid-configuration"), `${lang}: discovery`);
    assert.ok(all.includes("https://shop.sites.example.ir/callback"), `${lang}: the site's own return address`);
    assert.ok(all.includes("PKCE"), `${lang}: PKCE`);
    for (const step of steps) assert.doesNotMatch(step.title, /\.$/, "a title has no full stop (CLI-44)");
  }
  assert.notDeepEqual(loginGuideSteps("fa", numbers).map(s => s.title), loginGuideSteps("en", numbers).map(s => s.title));
});

test("CLI-97 the samples are Node, Python, PHP and PocketBase, with this server and never a real secret", () => {
  const samples = loginSamples(loginAddresses(numbers));
  const names = samples.map(sample => sample.name).join(" ");
  for (const name of ["Node", "Python", "PHP", "PocketBase"]) assert.ok(names.includes(name), name);
  for (const sample of samples) {
    assert.ok(sample.code.includes("https://example.ir"), sample.name);
    assert.ok(sample.code.includes("shop"), `${sample.name} uses the neutral site name (CLI-67)`);
    assert.doesNotMatch(sample.code, /secret_[A-Za-z0-9]{8}|dalan_[A-Za-z0-9]/);
  }
  for (const sample of proxySamples()) assert.ok(sample.code.includes("X-Dalan-User") || sample.code.includes("X_DALAN_USER"), sample.name);
});

test("CLI-97 the card's discovery address is under the issuer", () => {
  assert.equal(discoveryUrl("https://dalanet.ir/"), "https://dalanet.ir/.well-known/openid-configuration");
  assert.equal(loginAddresses({ ...numbers, sites_domain: "" }).site, "https://shop.<sites domain>");
});

test("AUTH-41 the consent screen posts the query as the site sent it, and the login keeps it", () => {
  const search = "?response_type=code&client_id=shop&redirect_uri=https%3A%2F%2Fshop.dalans.ir%2Fcb&state=" + "s".repeat(43)
    + "&code_challenge=" + "c".repeat(43) + "&code_challenge_method=S256&scope=openid%20profile";
  assert.equal(authorizeQuery(search), search.slice(1));
  assert.equal(pageFor("/oauth/authorize", false), "login");
  assert.equal(pageFor("/oauth/authorize", true), "authorize");
  assert.equal(loginTarget("/oauth/authorize", search, null), "/oauth/authorize" + search);
  // The GitHub round trip keeps it too (CLI-63), and a foreign address is still refused.
  assert.equal(safeNext("/oauth/authorize" + search), "/oauth/authorize" + search);
  assert.equal(safeNext("//evil.example"), null);
  assert.equal(loginTarget("/", "?next=//evil.example", null), "/bots");
});

test("AUTH-43 the consent screen says what the site gets, never the phone", () => {
  assert.deepEqual(consentItems(["openid"]), ["consentId"]);
  assert.deepEqual(consentItems(["openid", "profile", "email"]), ["consentId", "consentName"]);
  assert.equal(hostOf("https://shop.dalans.ir"), "shop.dalans.ir");
});
