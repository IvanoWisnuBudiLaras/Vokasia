import { test, expect } from "@playwright/test";

test.describe("Auth Login Flow", () => {
  test("SuperAdmin can login via login page and access /sa", async ({ page }) => {
    // 1. Masuk ke halaman login frontend
    await page.goto("/login");
    
    // 2. Klik tombol "Masuk ke Vokasia" yang memulai alur OAuth BFF
    await page.click('text="Masuk ke Vokasia"');

    // 3. Browser akan dialihkan ke form login backend (/account/login)
    await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
    await page.fill('input[name="email"]', "superadmin@vokasia.local");
    await page.fill('input[name="password"]', "DevPass123!");
    await page.click('button[type="submit"]');

    // 4. Setelah submit, harus berhasil kembali ke frontend dan mendarat di roleHome (/sa)
    await page.waitForURL((url) => url.pathname.startsWith("/sa"), { timeout: 15_000 });
    expect(page.url()).toContain("/sa");
  });

  test("TenantAdmin can login via login page and access /app", async ({ page }) => {
    await page.goto("/login");
    await page.click('text="Masuk ke Vokasia"');

    await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
    await page.fill('input[name="email"]', "admin@smkcontoh.local");
    await page.fill('input[name="password"]', "DevPass123!");
    await page.click('button[type="submit"]');

    await page.waitForURL((url) => url.pathname.startsWith("/app"), { timeout: 15_000 });
    expect(page.url()).toContain("/app");
  });

  test("Teacher can login via login page and access /app", async ({ page }) => {
    await page.goto("/login");
    await page.click('text="Masuk ke Vokasia"');

    await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
    await page.fill('input[name="email"]', "guru@smkcontoh.local");
    await page.fill('input[name="password"]', "DevPass123!");
    await page.click('button[type="submit"]');

    await page.waitForURL((url) => url.pathname.startsWith("/app"), { timeout: 15_000 });
    expect(page.url()).toContain("/app");
  });

  test("Student can login via login page and access /student", async ({ page }) => {
    await page.goto("/login");
    await page.click('text="Masuk ke Vokasia"');

    await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
    await page.fill('input[name="email"]', "siswa1@smkcontoh.local");
    await page.fill('input[name="password"]', "DevPass123!");
    await page.click('button[type="submit"]');

    await page.waitForURL((url) => url.pathname.startsWith("/student"), { timeout: 15_000 });
    expect(page.url()).toContain("/student");
  });

  test("Login with invalid password shows error on login form", async ({ page }) => {
    await page.goto("/login");
    await page.click('text="Masuk ke Vokasia"');

    await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
    await page.fill('input[name="email"]', "admin@smkcontoh.local");
    await page.fill('input[name="password"]', "WrongPassword123!");
    await page.click('button[type="submit"]');

    // Should stay on backend /account/login with error message displayed
    await page.waitForSelector('#login-error', { timeout: 10_000 });
    const errorText = await page.textContent('#login-error');
    expect(errorText).toContain("Email atau kata sandi salah");
  });
});
