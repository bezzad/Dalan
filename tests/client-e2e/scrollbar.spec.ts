// Rules: CLI-100
// In a real browser: a scrollbar's colour is clear until the mouse is over the part that scrolls; a parent under the mouse
// does not show the scrollbars of the parts inside it. A phone keeps the system's scrollbars (no rule applies).
import { expect, test } from "@playwright/test";

test("CLI-100 the scrollbar shows only over the part that scrolls", async ({ page, isMobile }) => {
  await page.setViewportSize({ width: 1400, height: 300 });
  await page.goto("/dashboard.html");
  const thumb = (selector: string) => page.locator(selector).first().evaluate(element => getComputedStyle(element).getPropertyValue("--thumb").trim());
  const list = ".page.workspace main > .scroller";
  if (isMobile) {
    expect(await thumb(list)).toBe("");
    return;
  }
  await page.mouse.move(1, 1);
  expect(await thumb(list)).toBe("transparent");
  const box = (await page.locator(list).boundingBox())!;
  await page.mouse.move(box.x + 20, box.y + 20);
  expect(await thumb(list)).not.toBe("transparent");
  // The side menu is under the same page, but its own scrollbar stays clear while the mouse is over the list.
  expect(await thumb("nav.side")).toBe("transparent");
});
