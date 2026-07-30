import { expect, test } from "bun:test";

test("service worker caches the offline shell at install, not on every navigation", async () => {
  const source = await Bun.file("public/sw.js").text();
  const installHandler = source.slice(
    source.indexOf('self.addEventListener("install"'),
    source.indexOf('self.addEventListener("activate"')
  );
  const fetchHandler = source.slice(source.indexOf('self.addEventListener("fetch"'));

  expect(installHandler).toContain("cacheOfflineShell()");
  expect(fetchHandler).not.toContain("cacheOfflineShell()");
});
