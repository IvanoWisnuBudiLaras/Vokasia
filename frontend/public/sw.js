const CACHE_NAME = "vokasia-offline-v2";
const OFFLINE_URL = "/offline";
const ICON_URLS = [
  "/icon.svg",
  "/icon-192.png",
  "/icon-512.png",
  "/icon-maskable-512.png",
];

async function cacheOfflineShell() {
  const cache = await caches.open(CACHE_NAME);
  const response = await fetch(OFFLINE_URL, {
    cache: "reload",
    credentials: "omit",
  });
  if (!response.ok) throw new Error("Halaman offline gagal disiapkan.");

  await cache.put(OFFLINE_URL, response.clone());
  const html = await response.text();
  const staticAssets = [...html.matchAll(/(?:src|href)="([^"]+)"/g)]
    .map((match) => match[1])
    .filter((url) => url.startsWith("/_next/static/"));
  await cache.addAll([...new Set([...ICON_URLS, ...staticAssets])]);
}

self.addEventListener("install", (event) => {
  event.waitUntil(
    (async () => {
      await cacheOfflineShell();
      await self.skipWaiting();
    })(),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    (async () => {
      const keys = await caches.keys();
      await Promise.all(
        keys
          .filter((key) => key.startsWith("vokasia-offline-") && key !== CACHE_NAME)
          .map((key) => caches.delete(key)),
      );
      await self.clients.claim();
    })(),
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== "GET" || url.origin !== self.location.origin) return;

  if (request.mode === "navigate") {
    event.respondWith(fetch(request).catch(() => caches.match(OFFLINE_URL)));
    return;
  }

  if (url.pathname.startsWith("/_next/static/") || ICON_URLS.includes(url.pathname)) {
    event.respondWith(
      caches.match(request).then(
        (cached) =>
          cached ??
          fetch(request).then((response) => {
            if (response.ok) {
              const copy = response.clone();
              void caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));
            }
            return response;
          }),
      ),
    );
  }
});
