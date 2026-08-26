# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: login.spec.ts >> Auth Login Flow >> Student can login via login page and access /student
- Location: tests\e2e\login.spec.ts:48:7

# Error details

```
TimeoutError: page.waitForURL: Timeout 15000ms exceeded.
=========================== logs ===========================
waiting for navigation until "load"
  navigated to "http://localhost:3000/login?error=access_denied"
============================================================
```

# Page snapshot

```yaml
- generic [active] [ref=f2e1]:
  - main [ref=f2e2]:
    - generic [ref=f2e3]:
      - generic [ref=f2e4]:
        - link "Vokasia Vokasia" [ref=f2e5] [cursor=pointer]:
          - /url: /
          - img "Vokasia" [ref=f2e6]
          - generic [ref=f2e7]: Vokasia
        - heading "Masuk ke Ruang Kerja" [level=1] [ref=f2e8]
        - paragraph [ref=f2e9]: Gunakan akun siswa, mentor, atau staf sekolah yang telah terdaftar.
      - alert [ref=f2e10]: Kamu tidak punya akses ke halaman itu.
      - generic [ref=f2e11]:
        - link "Masuk ke Vokasia" [ref=f2e12] [cursor=pointer]:
          - /url: /api/auth/login
        - link "← Kembali ke Beranda" [ref=f2e13] [cursor=pointer]:
          - /url: /
      - paragraph [ref=f2e15]:
        - text: Butuh verifikasi sertifikat?
        - link "Periksa di sini" [ref=f2e16] [cursor=pointer]:
          - /url: /verify
  - alert [ref=f2e17]
```

# Test source

```ts
  1  | import { test, expect } from "@playwright/test";
  2  | 
  3  | test.describe("Auth Login Flow", () => {
  4  |   test("SuperAdmin can login via login page and access /sa", async ({ page }) => {
  5  |     // 1. Masuk ke halaman login frontend
  6  |     await page.goto("/login");
  7  |     
  8  |     // 2. Klik tombol "Masuk ke Vokasia" yang memulai alur OAuth BFF
  9  |     await page.click('text="Masuk ke Vokasia"');
  10 | 
  11 |     // 3. Browser akan dialihkan ke form login backend (/account/login)
  12 |     await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
  13 |     await page.fill('input[name="email"]', "superadmin@vokasia.local");
  14 |     await page.fill('input[name="password"]', "DevPass123!");
  15 |     await page.click('button[type="submit"]');
  16 | 
  17 |     // 4. Setelah submit, harus berhasil kembali ke frontend dan mendarat di roleHome (/sa)
  18 |     await page.waitForURL((url) => url.pathname.startsWith("/sa"), { timeout: 15_000 });
  19 |     expect(page.url()).toContain("/sa");
  20 |   });
  21 | 
  22 |   test("TenantAdmin can login via login page and access /app", async ({ page }) => {
  23 |     await page.goto("/login");
  24 |     await page.click('text="Masuk ke Vokasia"');
  25 | 
  26 |     await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
  27 |     await page.fill('input[name="email"]', "admin@smkcontoh.local");
  28 |     await page.fill('input[name="password"]', "DevPass123!");
  29 |     await page.click('button[type="submit"]');
  30 | 
  31 |     await page.waitForURL((url) => url.pathname.startsWith("/app"), { timeout: 15_000 });
  32 |     expect(page.url()).toContain("/app");
  33 |   });
  34 | 
  35 |   test("Teacher can login via login page and access /app", async ({ page }) => {
  36 |     await page.goto("/login");
  37 |     await page.click('text="Masuk ke Vokasia"');
  38 | 
  39 |     await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
  40 |     await page.fill('input[name="email"]', "guru@smkcontoh.local");
  41 |     await page.fill('input[name="password"]', "DevPass123!");
  42 |     await page.click('button[type="submit"]');
  43 | 
  44 |     await page.waitForURL((url) => url.pathname.startsWith("/app"), { timeout: 15_000 });
  45 |     expect(page.url()).toContain("/app");
  46 |   });
  47 | 
  48 |   test("Student can login via login page and access /student", async ({ page }) => {
  49 |     await page.goto("/login");
  50 |     await page.click('text="Masuk ke Vokasia"');
  51 | 
  52 |     await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
  53 |     await page.fill('input[name="email"]', "siswa1@smkcontoh.local");
  54 |     await page.fill('input[name="password"]', "DevPass123!");
  55 |     await page.click('button[type="submit"]');
  56 | 
> 57 |     await page.waitForURL((url) => url.pathname.startsWith("/student"), { timeout: 15_000 });
     |                ^ TimeoutError: page.waitForURL: Timeout 15000ms exceeded.
  58 |     expect(page.url()).toContain("/student");
  59 |   });
  60 | 
  61 |   test("Login with invalid password shows error on login form", async ({ page }) => {
  62 |     await page.goto("/login");
  63 |     await page.click('text="Masuk ke Vokasia"');
  64 | 
  65 |     await page.waitForSelector('input[name="email"]', { timeout: 15_000 });
  66 |     await page.fill('input[name="email"]', "admin@smkcontoh.local");
  67 |     await page.fill('input[name="password"]', "WrongPassword123!");
  68 |     await page.click('button[type="submit"]');
  69 | 
  70 |     // Should stay on backend /account/login with error message displayed
  71 |     await page.waitForSelector('#login-error', { timeout: 10_000 });
  72 |     const errorText = await page.textContent('#login-error');
  73 |     expect(errorText).toContain("Email atau kata sandi salah");
  74 |   });
  75 | });
  76 | 
```