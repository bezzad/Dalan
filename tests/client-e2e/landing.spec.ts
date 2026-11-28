// Rules: CLI-26, CLI-103, CLI-104, CLI-105, CLI-106
// In a real browser: the home page in the look of ngrok's (owner, 2026-11-28): one call in the hero, the display font
// served by Dalan, a colour per card, "Try it now" with its install line, the strip, and no sideways scroll on a phone.
import { expect, test, type Page } from "@playwright/test";

async function open(page: Page, lang: "fa" | "en"): Promise<void> {
  await page.addInitScript(value => localStorage.setItem("dalan.lang", value), lang);
  await page.goto("/");
  await expect(page.locator(".hero h2")).toBeVisible();
}

for (const lang of ["en", "fa"] as const) {
  test(`CLI-26 one call in the hero; "Try it now" has the install line and both guides (${lang})`, async ({ page }) => {
    await open(page, lang);
    await expect(page.locator(".hero button, .hero a")).toHaveCount(1);
    const line = page.locator("#try .install .line code");
    await expect(line).toHaveText(/^\$ curl -fsSL http:\/\/localhost:\d+\/install\.sh \| sh$/);
    await page.locator("#try .systems button").nth(1).click();
    await expect(line).toHaveText(/^> irm http:\/\/localhost:\d+\/install\.ps1 \| iex$/);
    expect(await page.locator("#try a").evaluateAll(links => links.map(a => a.getAttribute("href")))).toEqual(["/guide/site", "/guide/bot"]);
  });

  test(`CLI-103 the hero title is in the display font, served by Dalan, with no sideways scroll (${lang})`, async ({ page }) => {
    await open(page, lang);
    await page.evaluate(() => document.fonts.ready);
    const fonts = await page.evaluate(() => performance.getEntriesByType("resource").map(entry => entry.name).filter(name => name.endsWith(".woff2")));
    const family = await page.locator(".hero h2").evaluate(element => getComputedStyle(element).fontFamily);
    // WebKit writes the names without quotes.
    expect(family).toMatch(/^"?Dalan Serif"?, "?Dalan Naskh"?,/);
    const font = lang === "fa" ? "Dalan Naskh" : "Dalan Serif";
    expect(await page.evaluate(name => document.fonts.check(`600 48px "${name}"`, name === "Dalan Naskh" ? "دالان" : "Dalan"), font)).toBe(true);
    expect(fonts.some(url => url.endsWith(lang === "fa" ? "/fonts/naskh.woff2" : "/fonts/serif.woff2")), fonts.join(" ")).toBe(true);
    for (const url of fonts) expect(new URL(url).origin).toBe(new URL(page.url()).origin);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("CLI-104 each card has its own colour; hover tints a plan and lights its button", async ({ page, isMobile }) => {
  await open(page, "en");
  const colours = (selector: string) => page.locator(selector).evaluateAll(items => items.map(item => getComputedStyle(item).color));
  const plans = await colours("#plans .badge-icon");
  expect(new Set(plans).size).toBe(2);
  expect(new Set(await colours("#offers .badge-icon")).size).toBe(4);
  test.skip(isMobile, "no hover on a phone");
  const card = page.locator("#plans .product").first();
  const before = await card.evaluate(element => getComputedStyle(element).backgroundImage);
  await card.hover();
  await expect.poll(() => card.evaluate(element => getComputedStyle(element).backgroundImage)).not.toBe(before);
  const button = card.locator("a.button");
  const [bg, text] = await button.evaluate(element => [getComputedStyle(element).backgroundColor, getComputedStyle(document.body).color]);
  expect(bg).toBe(text);
});

test("CLI-105 the strip stands still with reduced motion", async ({ page }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await open(page, "en");
  expect(await page.locator(".strip .track").evaluate(element => getComputedStyle(element).animationName)).toBe("none");
  await expect(page.locator(".strip ul[aria-hidden]")).toBeHidden();
});

test("CLI-106 the demo types its commands, and each page opened in its browser is a line in the log", async ({ page }) => {
  await open(page, "en");
  await page.locator(".demo").scrollIntoViewIfNeeded();
  const terminal = page.locator(".demo .terminal pre");
  await expect(terminal).toContainText("$ dalan http 3000 --name mysite", { timeout: 10_000 });
  await expect(terminal).toContainText(/Online: http:\/\/mysite\.localhost:\d+ {2}-> {2}http:\/\/localhost:3000/);
  const top = page.locator(".demo .log > div").first();
  await expect(top).toHaveText(/^\[\d\d:\d\d:\d\d\] GET \/ +200$/);
  await page.locator(".browser nav button", { hasText: "Projects" }).click();
  await expect(top).toHaveText(/^\[\d\d:\d\d:\d\d\] GET \/projects +200$/);
  await expect(page.locator(".browser nav button", { hasText: "Projects" })).toHaveAttribute("aria-current", "page");
});

test("CLI-106 with reduced motion the demo is all there at once; in Persian it is a mirror", async ({ page, isMobile }) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await open(page, "fa");
  await expect(page.locator(".demo .log > div")).toHaveCount(1);
  await expect(page.locator(".demo .terminal pre")).toContainText("$ node index.js &");
  test.skip(isMobile, "on a phone the terminal sits under the browser");
  const box = (selector: string) => page.locator(selector).evaluate(element => element.getBoundingClientRect().toJSON());
  const browser = await box(".browser");
  const terminal = await box(".demo .terminal");
  // English has the browser at the left and the terminal at the right corner; Persian the other way.
  expect(terminal.left).toBeLessThan(browser.left);
});
