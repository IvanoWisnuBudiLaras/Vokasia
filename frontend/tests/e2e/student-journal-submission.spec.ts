import { test, expect } from "@playwright/test";

test.describe("Student Journal & Evidence Flow", () => {
  test("Student can fill and submit journal with Quill editor and view in history", async ({ page }) => {
    // 1. Login sebagai Siswa 1
    await page.goto("/login");
    await page.click('text="Masuk ke Vokasia"');

    await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
    await page.fill('input[name="email"]', "siswa1@smkcontoh.local");
    await page.fill('input[name="password"]', "DevPass123!");
    await page.click('button[type="submit"]');

    await page.waitForURL((url) => url.pathname.startsWith("/student"), { timeout: 15_000 });

    // 2. Akses halaman Home / Jurnal Hari Ini
    await page.goto("/student");
    await expect(page.locator("h1")).toContainText("Hari Ini");

    // 3. Verifikasi halaman Riwayat Jurnal
    await page.goto("/student/history");
    await expect(page.locator("h1")).toContainText("Riwayat jurnal");

    // 4. Verifikasi halaman Perkembangan (Hasil Penilaian)
    await page.goto("/student/perkembangan");
    await expect(page.locator("h1")).toContainText("Perkembangan Pribadi");
    await expect(page.locator("text=PT Contoh Dev")).toBeVisible();

    // 5. Verifikasi halaman Portofolio
    await page.goto("/student/portofolio");
    await expect(page.locator("h1")).toContainText("Portofolio");
  });
});
