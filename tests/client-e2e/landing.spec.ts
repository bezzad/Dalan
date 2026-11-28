// Rules: CLI-26, CLI-103, CLI-104, CLI-105
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
    const fonts: string[] = [];
    page.on("request", request => { if (request.resourceType() === "font") fonts.push(request.url()); });
    await open(page, lang);
    await page.evaluate(() => document.fonts.ready);
    const family = await page.locator(".hero h2").evaluate(element => getComputedStyle(element).fontFamily);
    expect(family).toMatch(/^"Dalan Serif", "Dalan Naskh"/);
    const font = lang === "fa" ? "Dalan Naskh" : "Dalan Serif";
    expect(await page.evaluate(name => document.fonts.check(`600 48px "${name}"`, name === "Dalan Naskh" ? "دالان" : "Dalan"), font)).toBe(true);
    expect(fonts.length).toBeGreaterThan(0);
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
