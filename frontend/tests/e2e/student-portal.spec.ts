import { test, expect } from "@playwright/test";

test.describe("Student Portal Flow", () => {
  test.beforeEach(async ({ page }) => {
    // 1. Login sebagai Siswa 1
    await page.goto("/login");
    await page.click('text="Masuk ke Vokasia"');

    await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
    await page.fill('input[name="email"]', "siswa1@smkcontoh.local");
    await page.fill('input[name="password"]', "DevPass123!");
    await page.click('button[type="submit"]');

    await page.waitForURL((url) => url.pathname.startsWith("/student"), { timeout: 15_000 });
  });

  test("Student can view Home & Today's summary", async ({ page }) => {
    await page.goto("/student");
    await expect(page.locator("h1")).toContainText("Hari Ini");
  });

  test("Student can view History (/student/history) without 500 errors", async ({ page }) => {
    await page.goto("/student/history");
    await expect(page.locator("h1")).toContainText("Riwayat jurnal");
    
    // Tab filter "Disetujui" (status=1)
    await page.click('text="Disetujui"');
    await expect(page.locator("h1")).toContainText("Riwayat jurnal");
  });

  test("Student can view Guidance (/student/bimbingan)", async ({ page }) => {
    await page.goto("/student/bimbingan");
    await expect(page.locator("h1")).toContainText("Bimbingan");
  });

  test("Student can view Learning Records (/student/perkembangan)", async ({ page }) => {
    await page.goto("/student/perkembangan");
    await expect(page.locator("h1")).toContainText("Perkembangan");
  });

  test("Student can view Portfolio (/student/portofolio)", async ({ page }) => {
    await page.goto("/student/portofolio");
    await expect(page.locator("h1")).toContainText("Portofolio");
  });
});
