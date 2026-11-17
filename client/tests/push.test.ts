// Rules: CLI-21, CLI-22
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";
import { pushState } from "../src/push.ts";

test("CLI-21 what the notifications switch shows", () => {
  const ready = { key: "KEY", supported: true, permission: "default" as NotificationPermission, subscribed: false };

  assert.equal(pushState({ ...ready, key: null }), "hidden");
  assert.equal(pushState({ ...ready, supported: false }), "unsupported");
  assert.equal(pushState({ ...ready, permission: "denied" }), "blocked");
  assert.equal(pushState({ ...ready, permission: "granted", subscribed: true }), "on");
  assert.equal(pushState(ready), "off");
});

/** Runs the real service worker with a fake browser around it. */
function serviceWorker(windows: { url: string; focused: boolean }[]) {
  const listeners = new Map<string, (event: unknown) => void>();
  const shown: { title: string; options: NotificationOptions }[] = [];
  const opened: string[] = [];
  const clients = windows.map(window => ({
    ...window,
    messages: [] as unknown[],
    focusCalls: 0,
    postMessage(message: unknown) { this.messages.push(message); },
    async focus() { this.focusCalls++; return this; },
  }));
  const self = {
    location: { origin: "https://dalanet.ir" },
    addEventListener: (type: string, listener: (event: unknown) => void) => listeners.set(type, listener),
    registration: { showNotification: async (title: string, options: NotificationOptions) => void shown.push({ title, options }) },
    clients: {
      matchAll: async () => clients,
      openWindow: async (url: string) => void opened.push(url),
      claim: async () => {},
    },
    skipWaiting: () => {},
  };
  runInNewContext(readFileSync(new URL("../public/sw.js", import.meta.url), "utf8"), { self, URL, caches: {}, fetch });

  async function fire(type: string, event: object): Promise<void> {
    let work: Promise<unknown> = Promise.resolve();
    listeners.get(type)!({ ...event, waitUntil: (promise: Promise<unknown>) => { work = promise; } });
    await work;
  }
  return { fire, shown, opened, clients };
}

const note = { title: "Shop", body: "Your order is ready", url: "/@shopbot", tag: "bot-7" };
const push = { data: { json: () => note } };

test("CLI-22 no notification while a focused window shows that chat", async () => {
  const sw = serviceWorker([{ url: "https://dalanet.ir/@shopbot", focused: true }]);

  await sw.fire("push", push);

  assert.equal(sw.shown.length, 0);
});

test("CLI-22 a push while away shows a notification", async () => {
  const sw = serviceWorker([{ url: "https://dalanet.ir/@shopbot", focused: false }]);

  await sw.fire("push", push);

  assert.equal(sw.shown.length, 1);
  assert.equal(sw.shown[0]!.title, "Shop");
  assert.equal(sw.shown[0]!.options.body, "Your order is ready");
  assert.equal(sw.shown[0]!.options.tag, "bot-7");
});

test("CLI-22 a tap opens the chat in an open window and focuses it", async () => {
  const sw = serviceWorker([{ url: "https://dalanet.ir/", focused: false }]);
  const notification = { data: { url: "/@shopbot" }, close: () => {} };

  await sw.fire("notificationclick", { notification });

  // The message object comes from the worker's own realm, so compare it as JSON.
  assert.equal(JSON.stringify(sw.clients[0]!.messages), JSON.stringify([{ open: "/@shopbot" }]));
  assert.equal(sw.clients[0]!.focusCalls, 1);
});

test("CLI-22 a tap with no window opens a new one at the chat", async () => {
  const sw = serviceWorker([]);

  await sw.fire("notificationclick", { notification: { data: { url: "/@shopbot" }, close: () => {} } });

  assert.deepEqual(sw.opened, ["/@shopbot"]);
});
