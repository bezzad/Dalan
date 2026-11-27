// Rules: CLI-85, CLI-87, CLI-89, CLI-90, SEC-16
// In a real browser: the HTML viewer turns the sample report's pages and keeps it away from the app; the picture viewer;
// the big editor moves the same box; Enter by the pointer. Chromium on a computer and WebKit as an iPhone.
import { expect, test, type Page } from "@playwright/test";

const view = "http://127.0.0.1:4174";

async function open(page: Page): Promise<void> {
  await page.goto(`/harness.html?view=${encodeURIComponent(view)}`);
  await page.waitForFunction(() => (window as { ready?: boolean }).ready === true);
}

test("CLI-90 SEC-16 the report opens in a sandboxed frame, turns its pages, and reaches nothing of the app", async ({ page }) => {
  await open(page);
  await page.click("#open-html");
  const frameElement = page.locator(".viewer.html iframe");
  await expect(frameElement).toHaveAttribute("sandbox", "allow-scripts allow-popups");
  await expect(frameElement).toHaveAttribute("referrerpolicy", "no-referrer");
  const report = page.frameLocator(".viewer.html iframe");
  await expect(report.locator("body")).toHaveAttribute("data-page", "1");

  // The frame has the focus, so → reaches the report.
  await expect.poll(() => page.evaluate(() => document.activeElement?.tagName)).toBe("IFRAME");
  await page.keyboard.press("ArrowRight");
  await expect(report.locator("body")).toHaveAttribute("data-page", "2");

  // A swipe left inside the frame: the layer puts nothing over the frame, so the touches are the report's.
  const box = (await frameElement.boundingBox())!;
  const hit = await page.evaluate(([x, y]) => document.elementFromPoint(x!, y!)?.tagName, [box.x + box.width / 2, box.y + box.height / 2]);
  expect(hit).toBe("IFRAME");
  const frame = page.frames().find(f => f.url().startsWith(view))!;
  await frame.evaluate(() => {
    const touch = (x: number) => new Touch({ identifier: 1, target: document.body, clientX: x, clientY: 200 });
    document.body.dispatchEvent(new TouchEvent("touchstart", { touches: [touch(300)], changedTouches: [touch(300)], bubbles: true }));
    document.body.dispatchEvent(new TouchEvent("touchend", { touches: [], changedTouches: [touch(100)], bubbles: true }));
  });
  await expect(report.locator("body")).toHaveAttribute("data-page", "3");

  // SEC-16: the page's script got no cookie, no storage, no parent page, and its fetch to the app was blocked.
  await expect(report.locator("body")).toHaveAttribute("data-probe", /.+/);
  const probe = JSON.parse((await report.locator("body").getAttribute("data-probe"))!);
  // An opaque origin has no cookie jar: reading document.cookie throws (an empty answer would pass too).
  expect(probe.cookie).toMatch(/^(throws|empty)$/);
  expect(probe).toMatchObject({ localStorage: "throws", parent: "throws", fetch: "blocked" });

  // The tools: Close, Download, Open in a new tab. Esc (or leaving full screen) closes; the chat is as before.
  await expect(page.locator(".viewer.html .tools a[download]")).toHaveAttribute("href", "/icons/icon-512.png");
  await expect(page.getByRole("button", { name: "Open in a new tab" })).toBeVisible();
  // Full screen where the Fullscreen API works; elsewhere (iPhone) a fixed layer over the whole screen, the chat not scrolling.
  const fullscreen = await page.evaluate(() => Boolean(document.fullscreenEnabled || (document as { webkitFullscreenEnabled?: boolean }).webkitFullscreenEnabled));
  if (fullscreen) await expect(page.locator(".viewer.html")).toHaveClass(/full/);
  else {
    await expect(page.locator(".viewer.html")).not.toHaveClass(/full/);
    const layer = (await page.locator(".viewer.html").boundingBox())!;
    const screen = page.viewportSize()!;
    expect([layer.x, layer.y, layer.width, layer.height]).toEqual([0, 0, screen.width, screen.height]);
  }
  expect(await page.evaluate(() => getComputedStyle(document.body).overflow)).toBe("hidden");
  await page.getByRole("button", { name: "Close" }).click();
  await expect(page.locator(".viewer")).toHaveCount(0);
  await expect(page.locator("iframe")).toHaveCount(0);
  expect(await page.evaluate(() => document.body.classList.contains("viewing"))).toBe(false);
});

test("CLI-90 Esc closes the report viewer", async ({ page }) => {
  await open(page);
  await page.click("#open-html");
  await expect(page.locator(".viewer.html iframe")).toBeVisible();
  await page.locator("#open-html").evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.keyboard.press("Escape");
  await expect(page.locator(".viewer")).toHaveCount(0);
});

test("CLI-90 SEC-16 the view host refuses every other path", async ({ request }) => {
  expect((await request.get(`${view}/api/me`)).status()).toBe(404);
});

test("CLI-89 the picture viewer zooms on a double tap and closes with Esc, giving the focus back", async ({ page }) => {
  await open(page);
  await page.focus("#open-picture");
  await page.keyboard.press("Enter");
  const picture = page.locator(".viewer.picture img");
  await expect(picture).toBeVisible();
  await expect(page.locator(".viewer.picture .tools a[download]")).toBeVisible();
  await picture.dblclick();
  await expect(picture).toHaveAttribute("style", /scale\(2\.5\)/);
  await picture.dblclick();
  await expect(picture).toHaveAttribute("style", /scale\(1\)/);
  await page.keyboard.press("Escape");
  await expect(page.locator(".viewer")).toHaveCount(0);
  await expect(page.locator("#open-picture")).toBeFocused();
});

test("CLI-85 CLI-87 Enter by the pointer, and the big editor keeps the same box", async ({ page, isMobile }) => {
  await open(page);
  const box = page.locator("form.compose textarea");
  await box.click();
  await box.pressSequentially("Hi");
  await page.keyboard.press("Enter");
  if (isMobile) {
    // CLI-85: a coarse pointer adds a line; only Send sends.
    await expect(page.locator("#sent li")).toHaveCount(0);
    await expect(box).toHaveValue("Hi\n");
    await page.getByRole("button", { name: "Send" }).click();
  }
  await expect(page.locator("#sent li")).toHaveText(["Hi"]);
  await expect(box).toHaveValue("");

  if (!isMobile) {
    // Shift+Enter and Ctrl+Enter add a line; one undo takes the Ctrl+Enter line back.
    await box.pressSequentially("a");
    await page.keyboard.press("Shift+Enter");
    await box.pressSequentially("b");
    await page.keyboard.press("Control+Enter");
    await expect(box).toHaveValue("a\nb\n");
    await page.keyboard.press("ControlOrMeta+z");
    await expect(box).toHaveValue("a\nb");
    await box.fill("");
  }

  // CLI-87: 3 lines, no expand button; 4 lines, the button.
  await box.fill("1\n2\n3");
  await expect(page.locator("button.expand")).toBeHidden();
  await box.fill("1\n2\n3\n4");
  await expect(page.locator("button.expand")).toBeVisible();
  const before = await box.elementHandle();
  await page.locator("button.expand").click();
  const editor = page.locator("dialog.editor");
  await expect(editor).toBeVisible();
  // The very same element moved into the dialog.
  expect(await page.evaluate(b => document.querySelector("dialog.editor textarea") === b, before)).toBe(true);
  await editor.locator("textarea").press("End");
  await editor.locator("textarea").evaluate((t: HTMLTextAreaElement) => t.setSelectionRange(t.value.length, t.value.length));
  await page.keyboard.press(isMobile ? "Enter" : "Shift+Enter");
  await editor.locator("textarea").pressSequentially("5");
  // Ctrl+B puts ** around the selection.
  await editor.locator("textarea").evaluate((t: HTMLTextAreaElement) => t.setSelectionRange(t.value.length - 1, t.value.length));
  await page.keyboard.press("Control+b");
  await expect(editor.locator("textarea")).toHaveValue("1\n2\n3\n4\n**5**");
  await page.keyboard.press("Escape");
  await expect(editor).toHaveCount(0);
  await expect(box).toHaveValue("1\n2\n3\n4\n**5**");
  expect(await page.evaluate(b => document.querySelector("form.compose textarea") === b, before)).toBe(true);

  // Send from the editor: sent, closed, the box empty.
  await page.locator("button.expand").click();
  await page.locator("dialog.editor").getByRole("button", { name: "Send" }).click();
  await expect(page.locator("dialog.editor")).toHaveCount(0);
  await expect(page.locator("#sent li")).toHaveCount(2);
  await expect(box).toHaveValue("");
});
