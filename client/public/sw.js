// Service worker. It makes the app installable (CLI-09) and opens the app's own files when there is no network.
// API calls are never cached: messages come from the server, by live stream and sync.
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
