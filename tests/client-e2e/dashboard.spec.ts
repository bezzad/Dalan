// Rules: CLI-81
// In a real browser: on a wide window, a workspace page's list (My bots) must not lie over the dashboard's side menu.
import { expect, test } from "@playwright/test";

test("CLI-81 every side menu item is the top element where it is drawn, and takes the click", async ({ page, isMobile }) => {
  // Below 760px there is no side menu next to a page (the menu is its own page).
  test.skip(isMobile, "no side menu next to a page on a phone");
  for (const width of [1024, 1400, 1920]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/dashboard.html");
    for (const link of await page.locator("nav.side a").all()) {
      const box = (await link.boundingBox())!;
      const onTop = await link.evaluate((element, [x, y]) => element.contains(document.elementFromPoint(x!, y!)),
        [box.x + box.width / 2, box.y + box.height / 2]);
      expect(onTop, `${await link.textContent()} at ${width}px`).toBe(true);
    }
    await page.locator("nav.side a", { hasText: "My apps" }).click({ timeout: 2000 });
    await expect(page).toHaveURL(/#apps$/);
  }
});
