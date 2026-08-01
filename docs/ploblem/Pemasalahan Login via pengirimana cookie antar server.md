# Permasalahan Login via Pengiriman Cookie Antar Server

> **Tanggal**: 31 Juli – 1 Agustus 2026
> **Konteks**: Vokasia — SaaS manajemen PKL SMK
> **Arsitektur**: Next.js 16 BFF (port 3000) ↔ ASP.NET 10 + OpenIddict (port 5000)
> **Protokol Autentikasi**: OAuth 2.0 Authorization Code + PKCE

---

## Daftar Isi

1. [Latar Belakang Masalah](#1-latar-belakang-masalah)
2. [Gejala yang Tampak di Console Browser](#2-gejala-yang-tampak-di-console-browser)
3. [Investigasi: Menelusuri Alur dari Awal](#3-investigasi-menelusuri-alur-dari-awal)
4. [Peta Masalah yang Ditemukan](#4-peta-masalah-yang-ditemukan)
5. [Solusi per Masalah](#5-solusi-per-masalah)
6. [Arsitektur Login Setelah Perbaikan](#6-arsitektur-login-setelah-perbaikan)
7. [Arsitektur Logout Setelah Perbaikan](#7-arsitektur-logout-setelah-perbaikan)
8. [File yang Diubah](#8-file-yang-diubah)
9. [Pelajaran](#9-pelajaran)

---

## 1. Latar Belakang Masalah

Sistem Vokasia menggunakan arsitektur **BFF (Backend for Frontend)** di mana:

- **Frontend** (Next.js, port `3000`) bertanggung jawab menampilkan UI dan menyimpan token di Redis server-side.
- **Backend** (ASP.NET + OpenIddict, port `5000`) bertanggung jawab autentikasi, otorisasi, dan menerbitkan token OAuth.

Kedua server ini berjalan di **port berbeda** (`3000` vs `5000`). Ini berarti dari sudut pandang browser, keduanya adalah **origin yang berbeda**. Cookie yang dibuat oleh port 5000 tidak bisa dibaca oleh port 3000, dan sebaliknya. Masalah dimulai di sini.

### Mengapa Dua Server?

```
┌─────────────────────┐     ┌─────────────────────┐
│   Next.js (3000)    │     │  ASP.NET (5000)     │
│                     │     │                     │
│  • Halaman UI       │     │  • OpenIddict OAuth │
│  • BFF Proxy        │◄───►│  • REST API         │
│  • Redis Session    │     │  • Form Login HTML  │
│  • Cookie: vok_sess │     │  • Cookie:          │
│                     │     │    .AspNetCore.      │
│                     │     │    Cookies           │
└─────────────────────┘     └─────────────────────┘
       Browser                    Browser
    (origin A)                 (origin B)
```

Browser memperlakukan `localhost:3000` dan `localhost:5000` sebagai **dua origin berbeda**. Inilah yang membuat cookie, Content Security Policy (CSP), dan redirect menjadi rumit.

---

## 2. Gejala yang Tampak di Console Browser

Saat membuka DevTools dan mencoba login, console browser menampilkan **error merah** berikut:

### Error 1: CSP Memblokir Koneksi
```
Refused to connect to 'http://localhost:5000/...'
because it violates the following Content Security Policy directive:
"default-src 'none'"
```

**Apa artinya**: Browser menolak SEMUA koneksi keluar dari halaman login backend karena header CSP terlalu ketat (`default-src 'none'` tanpa `connect-src`).

### Error 2: CSP Memblokir Form Submit
```
Sending form data to 'http://localhost:5000/account/login'
violates the following Content Security Policy directive:
"form-action 'self'"
```

**Apa artinya**: Halaman frontend (port 3000) memiliki form yang mengarah ke backend (port 5000). Karena `form-action 'self'` hanya mengizinkan submit ke origin yang sama (port 3000), browser memblokir pengiriman form ke port 5000.

### Error 3: Browser Terjebak di Port 5000
Setelah login berhasil, browser tidak kembali ke port 3000. Pengguna "terjebak" di `http://localhost:5000/` — melihat halaman kosong atau error, bukan dashboard yang seharusnya.

### Error 4: Logout Tidak Bersih
Cookie `.AspNetCore.Cookies` tidak terhapus saat logout. Akibatnya, backend masih menganggap pengguna terautentikasi meskipun sesi Redis sudah dihapus.

---

## 3. Investigasi: Menelusuri Alur dari Awal

### Langkah 1: Membaca Log Server Backend

Saat menjalankan `dotnet run`, terminal backend menampilkan log OpenIddict yang sangat detail. Dari sini kita bisa menelusuri setiap langkah:

```
[Langkah 1] Browser → GET /connect/authorize
info: OpenIddict.Server — The authorization request was successfully extracted:
  client_id: "vokasia-bff"
  response_type: "code"
  redirect_uri: "http://localhost:3000/api/auth/callback"
  code_challenge_method: "S256"
```

↑ BFF mengirim browser ke backend untuk minta kode otorisasi.

```
[Langkah 2] Backend → 302 ke /account/login?returnUrl=...
```

↑ Karena belum ada cookie `.AspNetCore.Cookies`, backend me-redirect ke form login.

```
[Langkah 3] POST /account/login → SignIn → 303 See Other
info: [POST LOGIN DEBUG] Email=admin@vokasia.id, UserFound=True, PasswordOk=True
```

↑ Password benar, backend membuat cookie `.AspNetCore.Cookies` dan mengirim `303 See Other` ke `returnUrl`.

```
[Langkah 4] GET /connect/authorize (dengan cookie) → 302 ke callback
info: OpenIddict.Server — The authorization response was successfully returned to
  'http://localhost:3000/api/auth/callback' using the query response mode:
  { "code": "[redacted]", "state": "..." }
```

↑ **Titik kritis**: Backend mengirim browser KEMBALI ke port 3000 dengan kode otorisasi.

```
[Langkah 5] BFF /api/auth/callback → POST /connect/token (server-to-server)
info: OpenIddict.Server — The token request was successfully extracted:
  grant_type: "authorization_code"
  code_verifier: "wqcXxd6a..."
```

↑ BFF menukar kode + PKCE verifier menjadi access token + refresh token.

### Langkah 2: Membuat Flowchart Searah

Dari log di atas, alur login yang SEHARUSNYA terjadi:

```
Browser (3000)          BFF (3000)           Backend (5000)
     │                     │                      │
     ├──── /login ────────►│                      │
     │                     ├── redirect ──────────►│ /connect/authorize
     │                     │                      │
     │◄────────────────────┤◄── 302 ──────────────┤ → /account/login
     │                     │                      │
     ├── email+password ──►│                      │
     │                     ├── POST ──────────────►│ /account/login
     │                     │                      │ ✓ set cookie
     │                     │                      │ ✓ 303 → returnUrl
     │                     │                      │
     │                     │◄── 302 ──────────────┤ /connect/authorize
     │                     │   Location:          │ → code+state
     │                     │   localhost:3000/     │
     │                     │   api/auth/callback   │
     │                     │                      │
     │◄── set vok_sess ───┤── POST token ────────►│ /connect/token
     │    redirect to      │◄── access+refresh ───┤
     │    /student atau    │   (server-to-server)  │
     │    /app atau        │                      │
     │    /mentor          │                      │
     └────────────────────►│ Dashboard            │
```

### Langkah 3: Menemukan Titik Patah

Dengan membandingkan flowchart ideal vs perilaku nyata, ditemukan bahwa:

1. **Setelah POST /account/login berhasil**, backend mengirim `303 See Other` ke `returnUrl`. Tapi kalau `returnUrl` kosong atau fallback, backend mengarahkan ke `http://localhost:5000/` — bukan `http://localhost:3000/`.
2. **CSP backend** mengirim `default-src 'none'` tanpa `connect-src`, memblokir segala koneksi.
3. **CSP frontend** mengirim `form-action 'self'` yang memblokir form submit ke port 5000.
4. **Cookie `.AspNetCore.Cookies`** tidak dihapus saat logout karena `Path` tidak cocok.

---

## 4. Peta Masalah yang Ditemukan

| # | Masalah | Lokasi | Dampak |
|---|---------|--------|--------|
| 1 | Fallback redirect ke `/` (port 5000) bukan ke port 3000 | `AccountEndpoints.cs` → `PostLogin` | Browser terjebak di port 5000 |
| 2 | User terautentikasi yang akses `/account/login` tanpa `returnUrl` tidak diarahkan ke frontend | `AccountEndpoints.cs` → `GetLoginForm` | Loop di backend |
| 3 | `default-src 'none'` tanpa `connect-src` di halaman login | `SecurityHeadersMiddleware.cs` | Browser blokir semua koneksi |
| 4 | `form-action 'self'` di frontend tanpa izin ke port 5000 | `securityHeaders.ts` | Browser blokir form submit |
| 5 | Logout tidak menghapus cookie `.AspNetCore.Cookies` | `AccountEndpoints.cs` → `GetLogout` | Sesi zombie |
| 6 | Antiforgery token memblokir request logout | `MapAccountEndpoints` | Logout gagal 400 |
| 7 | Cookie auth tidak punya `Path=/` eksplisit | `IdentitySetup.cs` | `Delete()` gagal match path |
| 8 | `Frontend:PublicUrl` tidak ada di `appsettings.json` | `appsettings.json` | Backend tidak tahu URL frontend |

---

## 5. Solusi per Masalah

### Masalah 1 & 2: Browser Terjebak di Port 5000

**Akar masalah**: Saat `returnUrl` bernilai fallback (`/`), backend me-redirect ke `http://localhost:5000/` — bukan ke frontend.

**Solusi**: Jika `returnUrl` = fallback, panggil `ContinueToFrontend()` yang membaca `Frontend:PublicUrl` dari konfigurasi dan redirect ke `http://localhost:3000/api/auth/login`.

```csharp
// AccountEndpoints.cs — PostLogin (setelah login berhasil)
if (returnUrl == SafeFallbackReturnUrl)
{
    var config = req.HttpContext.RequestServices
        .GetRequiredService<IConfiguration>();
    return ContinueToFrontend(config);  // → http://localhost:3000/api/auth/login
}
return SeeOther(returnUrl);

// AccountEndpoints.cs — GetLoginForm (user sudah punya cookie)
if (context.User.Identity?.IsAuthenticated == true)
{
    if (rawReturnUrl == SafeFallbackReturnUrl)
    {
        var config = context.RequestServices
            .GetRequiredService<IConfiguration>();
        return ContinueToFrontend(config);
    }
    return SeeOther(rawReturnUrl);
}
```

### Masalah 3: CSP Backend Terlalu Ketat

**Akar masalah**: `SecurityHeadersMiddleware` mengirim `default-src 'none'` untuk semua halaman, termasuk halaman login HTML. Tanpa `connect-src`, browser memblokir semua koneksi.

**Solusi**: Tambah `connect-src`, `img-src`, dan deteksi environment Development.

```csharp
// SecurityHeadersMiddleware.cs
var connectSrc = _isDevelopment
    ? "connect-src 'self' http://localhost:3000 http://localhost:5000"
    : "connect-src 'self'";

csp =
    $"default-src 'none'; " +
    $"style-src 'nonce-{nonce}'; " +
    $"script-src 'nonce-{nonce}'; " +
    $"img-src 'self'; " +
    $"{connectSrc}; " +
    "form-action 'self'; " +
    "base-uri 'none'; frame-ancestors 'none'";
```

### Masalah 4: CSP Frontend Blokir Form ke Port 5000

**Akar masalah**: Frontend CSP `form-action 'self'` hanya mengizinkan form submit ke `localhost:3000`.

**Solusi**: Tambah `http://localhost:5000` di development.

```typescript
// securityHeaders.ts
`form-action 'self'${isProduction ? "" : " http://localhost:5000"}`
```

### Masalah 5, 6, 7: Logout Tidak Bersih

**Akar masalah tiga lapis**:
1. Endpoint logout dilindungi antiforgery — request dari frontend ditolak.
2. `context.SignOutAsync()` saja tidak cukup jika `Cookie.Path` tidak cocok.
3. Cookie `.AspNetCore.Cookies` tidak dihapus secara eksplisit.

**Solusi**:

```csharp
// MapAccountEndpoints — Hapus antiforgery di logout
app.MapGet("/account/logout", GetLogout).DisableAntiforgery();
app.MapPost("/account/logout", GetLogout).DisableAntiforgery();

// IdentitySetup.cs — Tetapkan Path eksplisit
options.LogoutPath = "/account/logout";
options.Cookie.Path = "/";

// GetLogout — Hapus paksa semua kemungkinan nama cookie
await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
var cookieOpts = new CookieOptions
    { Path = "/", HttpOnly = true, SameSite = SameSiteMode.Lax };
context.Response.Cookies.Delete(".AspNetCore.Cookies", cookieOpts);
context.Response.Cookies.Delete("Cookies", cookieOpts);
context.Response.Cookies.Delete(".AspNetCore.Identity.Application", cookieOpts);
```

Dan di sisi frontend (`logout/route.ts`):

```typescript
// Ikut buang cookie backend dari response frontend
res.cookies.set(".AspNetCore.Cookies", "", clearOptions);
res.cookies.set(".AspNetCore.Identity.Application", "", clearOptions);
res.cookies.set("Cookies", "", clearOptions);
```

### Masalah 8: Backend Tidak Tahu URL Frontend

**Solusi**: Tambah konfigurasi eksplisit di `appsettings.json`.

```json
{
  "Frontend": {
    "PublicUrl": "http://localhost:3000"
  }
}
```

---

## 6. Arsitektur Login Setelah Perbaikan

```
 ┌──────────┐                ┌──────────┐                ┌──────────┐
 │ Browser  │                │ Next.js  │                │ ASP.NET  │
 │          │                │ BFF:3000 │                │ API:5000 │
 └────┬─────┘                └────┬─────┘                └────┬─────┘
      │                          │                            │
      │  1. GET /login           │                            │
      ├─────────────────────────►│                            │
      │                          │                            │
      │  2. 302 → /api/auth/login│                            │
      │◄─────────────────────────┤                            │
      │                          │                            │
      │  3. GET /api/auth/login  │                            │
      ├─────────────────────────►│                            │
      │                          │  4. Buat PKCE (verifier    │
      │                          │     + challenge) simpan    │
      │                          │     di Redis               │
      │                          │                            │
      │  5. 302 → localhost:5000/connect/authorize            │
      │◄─────────────────────────┤                            │
      │                          │                            │
      │  6. GET /connect/authorize (tanpa cookie backend)     │
      ├──────────────────────────────────────────────────────►│
      │                          │                            │
      │  7. 302 → /account/login?returnUrl=/connect/authorize │
      │◄──────────────────────────────────────────────────────┤
      │                          │                            │
      │  8. GET /account/login   │                            │
      ├──────────────────────────────────────────────────────►│
      │                          │   9. Render form HTML      │
      │◄──────────────────────────────────────────────────────┤
      │                          │      CSP: connect-src      │
      │                          │      'self' localhost:3000  │
      │                          │      localhost:5000         │
      │                          │                            │
      │  10. POST /account/login (email + password)           │
      ├──────────────────────────────────────────────────────►│
      │                          │                            │
      │                          │   11. Validasi password    │
      │                          │       ✓ Set cookie:        │
      │                          │       .AspNetCore.Cookies  │
      │                          │                            │
      │  12. 303 See Other → returnUrl (/connect/authorize)   │
      │◄──────────────────────────────────────────────────────┤
      │                          │                            │
      │  13. GET /connect/authorize (DENGAN cookie backend)   │
      ├──────────────────────────────────────────────────────►│
      │                          │                            │
      │  14. 302 → localhost:3000/api/auth/callback?code=...  │
      │◄──────────────────────────────────────────────────────┤
      │         ╔══════════════════════════════════════╗       │
      │         ║  TITIK KRITIS: browser KELUAR dari   ║       │
      │         ║  port 5000, KEMBALI ke port 3000     ║       │
      │         ╚══════════════════════════════════════╝       │
      │                          │                            │
      │  15. GET /api/auth/callback?code=...&state=...        │
      ├─────────────────────────►│                            │
      │                          │  16. POST /connect/token   │
      │                          │      (server-to-server)    │
      │                          ├───────────────────────────►│
      │                          │                            │
      │                          │  17. ← access_token +      │
      │                          │       refresh_token        │
      │                          │◄───────────────────────────┤
      │                          │                            │
      │                          │  18. Simpan token di Redis │
      │                          │      Set cookie: vok_sess  │
      │                          │                            │
      │  19. 302 → /student (atau /app, /mentor, /sa)         │
      │◄─────────────────────────┤                            │
      │                          │                            │
      │  20. Dashboard dimuat    │                            │
      ├─────────────────────────►│  21. Proxy + Bearer token  │
      │                          ├───────────────────────────►│
      │                          │◄───────────────────────────┤
      │◄─────────────────────────┤                            │
      │    ✅ SELESAI             │                            │
```

---

## 7. Arsitektur Logout Setelah Perbaikan

```
 ┌──────────┐                ┌──────────┐                ┌──────────┐
 │ Browser  │                │ Next.js  │                │ ASP.NET  │
 │          │                │ BFF:3000 │                │ API:5000 │
 └────┬─────┘                └────┬─────┘                └────┬─────┘
      │                          │                            │
      │  1. POST /api/auth/logout│                            │
      ├─────────────────────────►│                            │
      │                          │                            │
      │                          │  2. Hapus sesi dari Redis  │
      │                          │     (access + refresh      │
      │                          │      token musnah)         │
      │                          │                            │
      │                          │  3. POST /connect/revoke   │
      │                          │     (cabut refresh token   │
      │                          │      di database backend)  │
      │                          ├───────────────────────────►│
      │                          │◄───────────────────────────┤
      │                          │                            │
      │  4. Response:            │                            │
      │     • Set-Cookie: vok_sess=""; Max-Age=0              │
      │     • Set-Cookie: vok_session=""; Max-Age=0           │
      │     • Set-Cookie: .AspNetCore.Cookies=""; Max-Age=0   │
      │     • 303 → localhost:5000/account/logout             │
      │◄─────────────────────────┤                            │
      │                          │                            │
      │  5. GET /account/logout  │                            │
      │     (DisableAntiforgery — tidak perlu token)          │
      ├──────────────────────────────────────────────────────►│
      │                          │                            │
      │                          │  6. SignOutAsync()          │
      │                          │     Delete .AspNetCore.     │
      │                          │     Cookies (Path=/)       │
      │                          │     Delete Cookies          │
      │                          │     Delete .AspNetCore.     │
      │                          │     Identity.Application   │
      │                          │                            │
      │  7. 303 → localhost:3000/login                        │
      │◄──────────────────────────────────────────────────────┤
      │                          │                            │
      │  8. Halaman login tanpa sesi apa pun                  │
      ├─────────────────────────►│                            │
      │    ✅ BERSIH              │                            │
```

---

## 8. File yang Diubah

| File | Perubahan |
|------|-----------|
| `backend/src/Vokasia.Api/Auth/AccountEndpoints.cs` | Fallback redirect ke frontend, hapus cookie eksplisit saat logout, disable antiforgery di logout |
| `backend/src/Vokasia.Api/Middleware/SecurityHeadersMiddleware.cs` | Tambah `connect-src`, `img-src`, deteksi environment, cakupan logout path |
| `backend/src/Vokasia.Api/Auth/IdentitySetup.cs` | Tambah `LogoutPath`, `Cookie.Path = "/"` |
| `backend/src/Vokasia.Api/appsettings.json` | Tambah `Frontend:PublicUrl` |
| `frontend/src/lib/securityHeaders.ts` | Tambah `http://localhost:5000` ke `form-action` di development |
| `frontend/src/app/api/auth/logout/route.ts` | Tambah penghapusan cookie `.AspNetCore.Cookies` di response |

---

## 9. Pelajaran

### 🔑 Pelajaran 1: Cookie Terikat pada Origin

Cookie yang dibuat oleh `localhost:5000` **tidak bisa dihapus** oleh `localhost:3000` secara langsung. Itulah mengapa logout harus melewati **dua tahap**: frontend menghapus cookie-nya sendiri, lalu me-redirect browser ke backend agar backend menghapus cookie-nya sendiri.

### 🔑 Pelajaran 2: CSP Adalah Penjaga Gerbang Browser

`Content-Security-Policy` bukan dekorasi — browser benar-benar **menolak** semua request yang melanggar. Saat development dengan dua port, CSP harus secara eksplisit mengizinkan kedua origin. Di production (single domain + reverse proxy), `'self'` sudah cukup.

### 🔑 Pelajaran 3: `form-action` Berbeda dari `connect-src`

- `connect-src` → mengontrol `fetch()`, `XMLHttpRequest`, WebSocket
- `form-action` → mengontrol `<form>` HTML submit

Keduanya harus dikonfigurasi **terpisah**. Salah satu sering terlupa.

### 🔑 Pelajaran 4: `Cookie.Delete()` Butuh Path yang Cocok

Browser hanya menghapus cookie jika `Set-Cookie` response memiliki `Path` yang **persis sama** dengan cookie yang tersimpan. Jika cookie dibuat dengan `Path=/account` tapi di-delete dengan `Path=/`, browser **mengabaikan** instruksi delete. Maka `Cookie.Path = "/"` harus ditetapkan sejak awal di konfigurasi autentikasi.

### 🔑 Pelajaran 5: Antiforgery Token pada Logout = Jebakan

Antiforgery melindungi dari CSRF pada operasi sensitif. Tapi logout **harus selalu berhasil** — jika antiforgery token kedaluwarsa atau hilang, pengguna tidak bisa keluar. Ini lebih berbahaya daripada CSRF pada logout. Maka logout di-exclude dari antiforgery.

### 🔑 Pelajaran 6: Baca Log Server, Bukan Hanya Console Browser

Console browser hanya menunjukkan **gejala** (`Refused to connect...`). Log server backend menunjukkan **penyebab** — ke mana redirect dikirim, cookie apa yang dibuat, dan token apa yang diterbitkan. Debugging OAuth PKCE **harus** dari kedua sisi.

### 🔑 Pelajaran 7: Development ≠ Production untuk Keamanan

Konfigurasi keamanan (CSP, cookie flags, CORS) harus **berbeda** antara development dan production. Di development kita perlu kelonggaran untuk dua port. Di production, reverse proxy menyatukan semuanya ke satu domain — dan CSP bisa lebih ketat.

---

> **Catatan**: Dokumen ini ditulis sebagai catatan riwayat debugging agar bisa dipelajari kembali secara mandiri. Setiap masalah yang ditemukan berasal dari **error merah di console browser** yang kemudian ditelusuri ke akar penyebabnya di kode server.
