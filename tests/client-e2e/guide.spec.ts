// Rules: CLI-101
// In a real browser: Persian is the mirror of English (the column sits at the right edge, never in the middle), and on
// a computer the guide uses the page width: the step names at the start side, the open step beside them.
import { expect, test, type Page } from "@playwright/test";

const user = { id: 1, name: "Test", username: "test", email: "test@example.com", has_password: true, has_phone: true, admin: false, builder: "approved", has_github: false };
const answers: Record<string, unknown> = {
  "/api/me": user,
  "/api/public/settings": { history_messages: 100, history_days: 30, access_log_days: 180, file_days: 30, update_expiry_seconds: 60, site_url: "http://localhost:4173", bot_api_url: "http://localhost:4173", contact_max_length: 2000, version: "dev" },
  "/api/account/keys": { keys: [] },
  "/api/sites": { domain: "localhost", scheme: "http", tunnel_url: "", sites: [] },
  "/api/builder/bots": { bots: [] },
  // The app's own numbers: their defaults are enough here.
  "/api/settings": {},
  "/api/auth/settings": {},
  "/api/inbox": { notices: [], unread: 0 },
};

async function open(page: Page, path: string, lang: "fa" | "en"): Promise<void> {
  await page.route("**/api/**", route => {
    const body = answers[new URL(route.request().url()).pathname];
    return body === undefined ? route.fulfill({ status: 404, json: {} }) : route.fulfill({ json: body });
  });
  await page.addInitScript(value => localStorage.setItem("dalan.lang", value), lang);
  await page.goto(path);
}

/** Left and right of a box, and of the page column (the header). */
const box = (page: Page, selector: string) => page.locator(selector).first().evaluate(element => {
  const r = element.getBoundingClientRect();
  return { left: Math.round(r.left), right: Math.round(r.right), top: Math.round(r.top) };
});

for (const lang of ["en", "fa"] as const) {
  test(`CLI-101 the column starts at the ${lang === "fa" ? "right" : "left"} edge (${lang})`, async ({ page }) => {
    for (const path of ["/faq", "/guide/site"]) {
      await open(page, path, lang);
      await expect(page.locator(path === "/faq" ? ".content > .card" : "ol.stepper").first()).toBeVisible();
      const column = await box(page, "header.site");
      const title = await box(page, ".content > h2.title");
      if (lang === "fa") expect(Math.abs(title.right - column.right), path).toBeLessThanOrEqual(2);
      else expect(Math.abs(title.left - column.left), path).toBeLessThanOrEqual(2);
    }
  });
}

test("CLI-101 on a computer the step names are at the start side and the open step is a wide card beside them", async ({ page, isMobile }) => {
  test.skip(isMobile, "a phone keeps the cards one under another");
  for (const lang of ["en", "fa"] as const) {
    await page.setViewportSize({ width: 1440, height: 900 });
    await open(page, "/guide/site", lang);
    await expect(page.locator(".step-body")).toBeVisible();
    const column = await box(page, "header.site");
    const name = await box(page, ".step.open .step-head");
    const body = await box(page, ".step-body");
    expect(body.right - body.left, lang).toBeGreaterThan(600);
    if (lang === "fa") expect(name.left, lang).toBeGreaterThan(body.right);
    else expect(name.right, lang).toBeLessThan(body.left);
    expect(Math.max(name.right, body.right) - Math.min(name.left, body.left), lang).toBeGreaterThan(column.right - column.left - 4);
    // A press on the open name keeps it open: the card beside the list is never empty.
    await page.locator(".step.open .step-head").click();
    await expect(page.locator(".step-body")).toBeVisible();
  }
});

test("CLI-101 on a phone the steps are cards one under another, as wide as the page", async ({ page, isMobile }) => {
  test.skip(!isMobile, "only on a phone");
  await open(page, "/guide/site", "fa");
  await expect(page.locator(".step-body")).toBeVisible();
  const card = await box(page, ".step.open");
  const next = await box(page, ".step.open + .step");
  expect(next.top).toBeGreaterThan(card.top);
  expect(card.right - card.left).toBeGreaterThan((await page.evaluate(() => innerWidth)) - 40);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});
