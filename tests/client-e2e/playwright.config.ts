// The browser tests (m32 task 3.2): Chromium on a computer, and WebKit as an iPhone (close to iPhone Safari, not the same:
// the owner still opens a real report on an iPhone after the deploy).
import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: "*.spec.ts",
  timeout: 30_000,
  forbidOnly: true,
  reporter: "list",
  use: { baseURL: "http://localhost:4173", trace: "off" },
  webServer: { command: "node server.mjs", url: "http://localhost:4173/harness.html", reuseExistingServer: false },
  projects: [
    // PW_CHROMIUM: a Chromium already on the machine, for a run without the download (CI downloads its own).
    { name: "chromium", use: { ...devices["Desktop Chrome"], launchOptions: process.env.PW_CHROMIUM ? { executablePath: process.env.PW_CHROMIUM } : {} } },
    { name: "iphone", use: { ...devices["iPhone 15"] } },
  ],
});
