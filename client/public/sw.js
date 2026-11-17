// Service worker. It makes the app installable (CLI-09), opens the app's own files when there is no network,
// and shows pushes (CLI-22). API calls are never cached: messages come from the server, by live stream and sync.
const CACHE = "dalan-shell";

self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", event => event.waitUntil(self.clients.claim()));

self.addEventListener("fetch", event => {
  const url = new URL(event.request.url);
  if (event.request.method !== "GET" || url.origin !== self.location.origin
      || url.pathname.startsWith("/api/") || url.pathname.startsWith("/v1/")) {
    return;
  }

  // Network first, so a new release is used at once. The saved copy is only for "no network".
  event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    // Every page address ("/", "/@name") is the same page.
    const key = event.request.mode === "navigate" ? "/" : event.request;
    try {
      const response = await fetch(event.request);
      if (response.ok) await cache.put(key, response.clone());
      return response;
    } catch (error) {
      const saved = await cache.match(key);
      if (saved) return saved;
      throw error;
    }
  })());
});

/** Only a path of this site; anything else opens the bot list. */
function chatPath(url) {
  return typeof url === "string" && url.startsWith("/") && !url.startsWith("//") ? url : "/";
}

function windows() {
  return self.clients.matchAll({ type: "window", includeUncontrolled: true });
}

// CLI-22: a push is shown, unless a focused window already shows that chat.
self.addEventListener("push", event => event.waitUntil((async () => {
  const note = event.data ? event.data.json() : null;
  if (!note) return;
  const url = chatPath(note.url);
  if ((await windows()).some(client => client.focused && new URL(client.url).pathname === url)) return;

  await self.registration.showNotification(note.title, {
    body: note.body,
    tag: note.tag,
    renotify: true,
    icon: "/icons/icon-192.png",
    data: { url },
  });
})()));

// CLI-22: a tap opens the chat in a window that is open, or in a new one.
self.addEventListener("notificationclick", event => {
  event.notification.close();
  event.waitUntil((async () => {
    const url = chatPath(event.notification.data && event.notification.data.url);
    const open = (await windows())[0];
    if (!open) return self.clients.openWindow(url);
    open.postMessage({ open: url });
    return open.focus();
  })());
});
