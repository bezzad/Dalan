// Rules: CLI-86
// In a real browser: in a chat only the message list scrolls. The message box has no scrollbar until it reaches its
// largest size, and the page itself is never taller than the window.
import { expect, test } from "@playwright/test";

test("CLI-86 the box scrolls only past its largest size, and the page never scrolls", async ({ page }) => {
  await page.goto("/harness.html");
  await page.waitForFunction(() => (window as { ready?: boolean }).ready === true);
  const box = page.locator("form.compose textarea");
  const overflow = () => box.evaluate(element => element.scrollHeight - element.clientHeight);
  for (const text of ["", "سلام", "one\ntwo\nthree"]) {
    await box.fill(text);
    await box.dispatchEvent("input");
    expect(await overflow(), JSON.stringify(text)).toBeLessThanOrEqual(0);
  }
  await box.fill(Array.from({ length: 30 }, (_, i) => `line ${i}`).join("\n"));
  await box.dispatchEvent("input");
  expect(await overflow()).toBeGreaterThan(0);
  expect(await page.evaluate(() => document.documentElement.scrollHeight)).toBeLessThanOrEqual(await page.evaluate(() => innerHeight));
});
