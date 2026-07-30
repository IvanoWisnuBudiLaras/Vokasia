# Audit Vokasia v3 — Pengujian Total: Keamanan Runtime, UX, & Kesiapan Produksi

Audit ini berbeda dari v1/v2: seluruhnya **pengujian eksekusi nyata** terhadap instance yang berjalan — browser sungguhan (Playwright/Chromium), probing HTTP, dan uji serangan aktif. Bukan pembacaan kode.

**Tanggal**: 29 Juli 2026 · **Target**: Docker Compose (`frontend:3000`, `api:5000`) via `host.docker.internal`
**Perkakas**: Chromium headless 149 (Playwright), curl, analisis repo

Setiap klaim di bawah berasal dari perintah yang dijalankan pada sesi ini. Tidak ada yang bersumber dari ingatan sesi sebelumnya atau laporan pihak lain.

---

## 0. Cakupan — dibaca dulu

**Yang diuji (tuntas):** seluruh permukaan publik — 7 rute × 4 viewport, aksesibilitas, XSS, open redirect, antiforgery, rate limit, batas autentikasi, header keamanan, PWA, service worker, BFF proxy.

**Yang TIDAK diuji, dan tidak boleh dianggap lulus:**

1. **Seluruh area di balik login** (`/app`, `/sa`, `/mentor`, isi `/student`). Saya tidak mengautentikasi karena memasukkan kata sandi ke form mana pun berada di luar batas operasi saya — batas itu tetap berlaku walaupun kredensialnya milik seed demo. **Karena itu permintaan "semua fitur harus bekerja dengan baik" masih BELUM terjawab untuk area terautentikasi.** Yang sudah terbukti: pintu masuknya benar (semua rute terproteksi menolak akses anonim dengan benar).
2. **Suite test proyek** (`dotnet test`, `bun test`, `next build`) — `dotnet` dan `bun` tidak terpasang di sandbox. Angka 330/57 dari laporan Developer tidak diverifikasi ulang.
3. **Jalur sukses** `/verify/{kode-valid}` dan `/p/{slug-terpublish}` — tidak ada data uji valid.

**Cara menutup celah #1**: login manual di browser, lalu saya lanjutkan dengan sesi yang sudah aktif — saya tidak perlu menyentuh kata sandi sama sekali.

---

## 1. Ringkasan eksekutif

Permukaan publik Vokasia **lulus setiap uji yang saya jalankan** — termasuk uji serangan aktif. Tidak ada XSS, tidak ada open redirect, tidak ada bypass autentikasi, tidak ada kebocoran data. Responsif dan aksesibilitasnya bersih sempurna di 28 kombinasi rute×viewport. Ini hasil yang jarang saya lihat pada proyek solo-dev.

Masalahnya bukan di kode aplikasi. Masalahnya di **konfigurasi deployment**: `README-DEPLOY.md` — runbook produksi resmi proyek ini — menginstruksikan konfigurasi yang, jika diikuti apa adanya, menghasilkan produksi yang **login-nya rusak total** dan **beberapa proteksi keamanannya mati**. Ditambah satu celah autentikasi berdiri sendiri (password spraying).

**8 temuan wajib ditutup sebelum produksi.** Lima di antaranya berakar pada runbook/konfigurasi, bukan pada kode aplikasi — artinya perbaikannya murah.

---

## 2. Hasil uji yang LULUS (bukti eksekusi)

Disebut lebih dulu supaya proporsional. Semua diuji sesi ini.

### 2.1 Responsif — bersih sempurna
7 rute publik × 4 viewport (320/375/414/768px) = 28 kombinasi:

```
overflow horizontal : 0 dari 28
tap target < 44px   : 0 dari 28
```

Tidak ada satu pun pelanggaran. Ini memvalidasi klaim `DESIGN.md` soal target 44px dan dukungan layar 320px — dengan pengukuran, bukan asumsi.

### 2.2 Aksesibilitas — bersih di seluruh rute publik

```
ROUTE                 lang  img!alt  btn!nm  inp!lbl  a!nm  h1  skipHeading
/                     id    0        0       0        0     1   False
/login                id    0        0       0        0     1   False
/verify               id    0        0       0        0     1   False
/verify/BADCODE9      id    0        0       0        0     1   False
/p/tidak-ada-xyz      id    0        0       0        0     1   False
/mentor-invite        id    0        0       0        0     1   False
/rute-ngawur-404      id    0        0       0        0     1   False
```

`lang="id"` benar, nol gambar tanpa alt, nol tombol/link tanpa nama aksesibel, nol input tanpa label, tepat satu `h1` per halaman, tidak ada lompatan level heading.

### 2.3 XSS — tidak ada yang tereksekusi
Empat payload disuntik lewat parameter rute, dirender di browser sungguhan:

```
<img src=x onerror=alert(1)>   dialog=0  elemen ter-inject=0  -> AMAN
<script>alert(1)</script>      dialog=0  elemen ter-inject=0  -> AMAN
"><svg onload=alert(1)>        dialog=0  elemen ter-inject=0  -> AMAN
javascript:alert(1)            dialog=0  elemen ter-inject=0  -> AMAN
```

> **Catatan metodologi — sekaligus koreksi kesalahan saya sendiri.** Uji pertama saya (grep terhadap HTML mentah) sempat menghasilkan "TIDAK TER-ESCAPE (BAHAYA)". Itu **positif palsu**: payload memang muncul mentah di dalam *RSC flight payload* Next.js (`<script>self.__next_f.push(...)</script>`) — tapi di sana ia berstatus **data JSON**, bukan kode yang dieksekusi. Setelah detektor diperbaiki untuk mengabaikan script RSC dan mengukur yang benar-benar penting (dialog yang muncul + elemen yang benar-benar masuk DOM), hasilnya bersih. Saya catat ini karena hampir saja melaporkan kerentanan yang tidak ada.

### 2.4 Open redirect — tertutup rapat
Empat variasi payload pada `returnUrl`, semuanya dinormalisasi ke fallback aman:

```
https://situs-jahat.example/  -> value="/account/continue"
//situs-jahat.example/        -> value="/account/continue"
/\evil                        -> value="/account/continue"
%2f%2fevil.example            -> value="/account/continue"
```

Termasuk varian protocol-relative (`//`) dan backslash yang paling sering terlewat.

### 2.5 Antiforgery — menolak dengan benar
```
POST /account/login tanpa token -> 303, error="Form masuk sudah kedaluwarsa atau tidak valid."
```

### 2.6 Batas autentikasi — nol kebocoran
API langsung (8 endpoint, tanpa token) semuanya `401` dengan body kosong. BFF proxy juga tidak bisa dijadikan jalan pintas:

```
/api/proxy/students             -> 401 {"message":"Belum login."}
/api/proxy/sa/tenants           -> 401 {"message":"Belum login."}
/api/proxy/notifications        -> 401 {"message":"Belum login."}
/api/proxy/dashboard/school/x   -> 401 {"message":"Belum login."}
```

Rute terproteksi di frontend juga mengalihkan dengan benar: `/student`, `/app`, `/mentor`, `/sa` → `/login?error=unauthenticated&next=...`

### 2.7 Rate limit publik — persis sesuai dokumentasi
```
404 ×9, lalu 429 pada request ke-10
body: {"code":"rate-limit-exceeded","message":"Terlalu banyak percobaan. Coba lagi nanti."}
Retry-After: 60 · Content-Type: application/json
```

### 2.8 Toggle kata sandi — berfungsi penuh, aksesibel
```
type awal            : password
setelah klik         : text      · aria-label: "Sembunyikan kata sandi"
aria-live mengumumkan: "Kata sandi ditampilkan."
setelah klik ke-2    : password
```

### 2.9 Alur form verifikasi — berfungsi end-to-end
Isi kode → submit → `/verify/KODE-UJI-XYZ` → halaman hasil "Sertifikat tidak ditemukan". Navigasi, routing, dan tampilan hasil semuanya benar.

### 2.10 PWA & aset
Keempat ikon manifest benar-benar ada (`icon.svg` 561B, `icon-192.png` 3.6KB, `icon-512.png` 14.7KB, `icon-maskable-512.png` 10.1KB), `sw.js` tersedia dengan offline shell yang meng-cache `/offline` menggunakan `credentials: "omit"` — pilihan yang tepat, tidak ikut menyimpan respons ber-kredensial.

### 2.11 Header keamanan & permukaan info
Lengkap di dua origin. `/swagger`, `/hangfire`, `/.env`, `/appsettings.json` semuanya 404. `/health` hanya `Healthy`. Cookie antiforgery `httponly` + `samesite=strict`.

---

## 3. TEMUAN KRITIS — Runbook produksi menghasilkan deployment yang login-nya rusak

**Tingkat: kritis. Temuan baru sesi ini.**

`frontend/src/app/api/auth/login/route.ts:36` menentukan tujuan redirect OAuth untuk **browser pengguna**:

```
const apiPublicBase = process.env.API_PUBLIC_URL ?? "http://localhost:5000";
```

Variabel `API_PUBLIC_URL` **tidak disebut sama sekali** di `README-DEPLOY.md`:

```
===== Apakah API_PUBLIC_URL disebut di README-DEPLOY? =====
>>> TIDAK DISEBUT SAMA SEKALI
```

Daftar env di runbook produksi justru berisi:

```env
API_INTERNAL_URL=http://api:8080
Frontend__PublicUrl=http://localhost:3000     # <- literal localhost di runbook PRODUKSI
```

Konsekuensinya konkret: siapa pun yang mengikuti runbook akan mendapat deployment di mana tombol "Masuk" mengarahkan browser pengguna ke `http://localhost:5000` — yaitu komputer pengguna itu sendiri. **Login gagal untuk semua orang, 100%.** Ini bukan degradasi halus; aplikasinya tidak bisa dipakai sama sekali.

Perbaikan: tambahkan `API_PUBLIC_URL` dan `Frontend__PublicUrl` ke daftar env runbook dengan contoh domain sungguhan (`https://vokasia.sekolah.id`), bukan localhost.

---

## 4. TEMUAN KRITIS — Password spraying tidak dibatasi

**Tingkat: kritis.**

Rate limit login mempartisi dengan kunci `login:{ip}:{email}`, sehingga batas 5/menit hanya mengikat **satu email**.

Bukti — 12 percobaan, satu password, 12 email berbeda, satu IP:

```
303 303 303 303 303 303 303 303 303 303 303 303
>>> jumlah 429: 0 dari 12
```

Kontrol — email yang sama memang dibatasi benar:

```
303 303 303 303 303 429   (429 di percobaan ke-6)
```

Mekanismenya bekerja; cakupannya yang salah. Menutup *brute force* (banyak password → satu akun), membiarkan *password spraying* (satu password → banyak akun) terbuka penuh.

**Kenapa serius untuk Vokasia**: pola email di `DemoSeeder.cs` sepenuhnya dapat ditebak — `admin@{npsn}.vokasia.demo`, `guru{0..4}@{npsn}...`, `depthead@{npsn}...`. NPSN adalah data publik. Penyerang dapat menyusun daftar target tanpa menebak, lalu menyapu satu kata sandi lemah ke seluruh sekolah tanpa pernah tersentuh 429.

Perbaikan: tambahkan limiter kedua berbasis **IP saja** (mis. 20–30/menit) berdampingan dengan yang per-IP+email. Mana pun tercapai lebih dulu, tolak.

---

## 5. TEMUAN KRITIS — Runbook mengirim environment Development ke produksi

**Tingkat: kritis. Satu akar, empat akibat.**

`docker-compose.yml` menetapkan `ASPNETCORE_ENVIRONMENT: Development` untuk `api` (baris 93) dan `worker` (baris 129). `README-DEPLOY.md` baris 49 menginstruksikan `docker compose up -d` sebagai cara deploy VPS produksi, tanpa langkah mengganti environment.

**5a. HSTS mati di API** — `app.UseHsts()` dibungkus `if (!IsDevelopment())`. Terverifikasi live:
```
>>> TIDAK ada HSTS di API
```

**5b. OAuth diterima lewat HTTP polos** — `OpenIddictSetup.cs:130` memanggil `DisableTransportSecurityRequirement()` di dalam blok `IsDevelopment()`. Seluruh alur `/connect/*`, termasuk pertukaran authorization code dan refresh token, berjalan tanpa TLS.

**5c. Dokumen OpenAPI terbuka tanpa autentikasi**:
```
/openapi/v1.json -> 200, 72.993 byte
82 path · 41 schema/DTO terekspos
```
Termasuk peta lengkap superadmin: `/sa/tenants`, `/sa/plans`, `/sa/invoices/{id}/confirm-payment`, `/sa/companies/merge`, `/sa/audit-logs`, `/api/impersonation/end`. Swagger UI memang mati, dokumen mentahnya tidak.

**5d. Seeder demo tanpa guard environment**:
```
>>> tidak ada guard environment pada jalur seed
```
`Program.cs:161` menerima `seed demo` di environment apa pun, membuat akun admin/kepala jurusan/5 guru per tenant dengan kata sandi statis `Demo-Passw0rd!` (`DemoSeeder.cs:254`) — kata sandi yang tertulis di repositori. Runbook mencantumkan perintah ini sebagai langkah nomor 4.

Perbaikan: `ASPNETCORE_ENVIRONMENT: Production` di compose produksi (atau `docker-compose.prod.yml` terpisah), plus guard yang menolak `seed demo` di luar Development.

---

## 6. TEMUAN TINGGI — Belum siap di belakang reverse proxy

`README-DEPLOY.md:25` menyebut "any proxy layers (e.g. Nginx/Caddy)" — topologi standar untuk TLS. Tapi:

```
>>> TIDAK ADA konfigurasi ForwardedHeaders
```

Tiga akibat:

- **Cookie tanpa flag `Secure`.** `Program.cs:32` memakai `CookieSecurePolicy.SameAsRequest`; di belakang proxy request internal berupa HTTP. Terkonfirmasi di respons live: `Set-Cookie: vok_antiforgery=...; path=/; samesite=strict; httponly` — tanpa `secure`.
- **Rate limiting jadi rusak.** Partisi memakai `Connection.RemoteIpAddress`, yang di belakang proxy bernilai IP proxy — sama untuk semua pengunjung. **Rate limit yang saya buktikan bekerja di §2.7 akan berhenti berfungsi benar begitu di-deploy di belakang proxy**: semua pengguna berbagi satu bucket.
- **Potensi redirect loop** dari `UseHttpsRedirection()` karena aplikasi selalu melihat HTTP.

---

## 7. TEMUAN TINGGI — Kunci kriptografi tidak persisten

**7a. DataProtection tidak dikonfigurasi sama sekali.** Pencarian `AddDataProtection|PersistKeysTo|SetApplicationName` di seluruh `backend/src`: nol hasil. Akibatnya setiap restart container membatalkan seluruh token antiforgery dan cookie terenkripsi — semua pengguna terlempar keluar, form yang terbuka gagal submit. Multi-instance mustahil.

**7b. Sertifikat OpenIddict masih Development, tanpa cabang produksi**:
```
OpenIddictSetup.cs:110  options.AddDevelopmentEncryptionCertificate()
OpenIddictSetup.cs:111         .AddDevelopmentSigningCertificate();
```
Tidak dibungkus pemeriksaan environment. Komentar di atasnya sudah menyadari ("Prod: ganti dengan sertifikat X.509"), kodenya belum menindaklanjuti. Token OAuth batal tiap restart.

---

## 8. TEMUAN MENENGAH — Kontrak error tidak seragam

Menjawab langsung pertanyaanmu, "error apa muncul berbentuk JSON": **mayoritas tidak.**

```
/api/notifications              -> 401  Content-Length: 0  (tanpa content-type)
/api/verify/KODE-TIDAK-ADA      -> 404  Content-Length: 0
/p/slug-tidak-ada               -> 404  Content-Length: 0
/api/journals/bukan-guid/submit -> 404  Content-Length: 0
/sa/companies/merge             -> 401  Content-Length: 0
```

Hanya 429 (dan BFF proxy, yang benar) yang mengembalikan JSON. `AddProblemDetails()` sudah terdaftar, tapi 401 dari middleware autentikasi dan 404 dari routing tidak melewati exception handler sehingga tidak pernah mendapat body.

Perbaikan: pasang `StatusCodePages` agar 401/403/404 ikut menghasilkan ProblemDetails JSON yang seragam.

---

## 9. TEMUAN RENDAH

**9a. `robots.txt` dan `sitemap.xml` tidak ada** (keduanya 404). Untuk Vokasia ini menyentuh privasi, bukan sekadar SEO: `/p/{slug}` adalah portofolio siswa — sebagian di antaranya anak di bawah umur. Tanpa `robots.txt`, tidak ada kendali sama sekali atas apakah mesin pencari mengindeks halaman itu. Perlu keputusan sadar (diizinkan atau tidak), bukan dibiarkan default.

**9b. `X-Powered-By: Next.js` bocor** di setiap respons frontend. `poweredByHeader` tidak di-set di `next.config.ts`. Satu baris untuk menutup.

**9c. CSP frontend longgar.** `script-src 'self' 'unsafe-inline'` menghilangkan sebagian besar proteksi XSS dari CSP; `connect-src` dan `img-src` mengizinkan `http:` ke origin mana pun. Alasannya terdokumentasi di `securityHeaders.ts` (URL presigned MinIO dinamis, halaman statis tanpa nonce) dan masuk akal secara teknis — tapi `http:` sebaiknya dibuang khusus di produksi. Catatan: proteksi nyata saat ini datang dari React escaping (terbukti di §2.3), bukan dari CSP.

**9e. Pesan "Sesi berakhir" muncul untuk orang yang belum pernah login.** Ditemukan saat memeriksa Chrome secara langsung. `lib/guard.ts:49` mengirim `error=unauthenticated` untuk **setiap** akses tanpa autentikasi — termasuk pengunjung yang baru pertama kali membuka tautan. `app/login/page.tsx:10` memetakan kode itu ke satu pesan tunggal:

```
unauthenticated: "Sesi berakhir. Silakan masuk kembali."
```

Terverifikasi live: membuka `/app` tanpa pernah login sama sekali menghasilkan `{"user":null}` di `/api/auth/session`, lalu halaman login menampilkan "Sesi berakhir. Silakan masuk kembali." — memberi tahu pengguna bahwa sesinya habis padahal ia tidak pernah punya sesi.

Ini melanggar prinsip copy `DESIGN.md` proyek sendiri: *"Error = instruksi, bukan permintaan maaf. Urutan: apa yang salah → kenapa → apa yang harus dilakukan."* Pesan ini keliru pada bagian "apa yang salah". Untuk guru atau mentor yang menerima tautan lewat WhatsApp dan mengekliknya pertama kali, pesan itu membingungkan — mengesankan ada yang rusak, padahal ia hanya belum masuk.

**Perbaikan**: pisahkan dua kode error. `unauthenticated` (belum pernah masuk) → "Masuk dulu untuk membuka halaman ini."; `session_expired` (token kedaluwarsa, dikirim dari `callback/route.ts`) → tetap "Sesi berakhir. Silakan masuk kembali."

**9d. 227 file belum ter-commit.** `git log` terakhir masih `5b7d1bf` (VOK-H6-E2); seluruh hasil perbaikan v1–v3 belum masuk riwayat versi. Bukan temuan keamanan, tapi risiko operasional: tidak ada titik pulih dan tidak ada jejak audit perubahan.

---

## 10. Prioritas sebelum produksi

| # | Temuan | Tingkat | Effort |
|---|---|---|---|
| 1 | `API_PUBLIC_URL` + `Frontend__PublicUrl` di runbook (§3) | Kritis | Sangat kecil |
| 2 | Limiter per-IP untuk cegah password spraying (§4) | Kritis | Kecil |
| 3 | `ASPNETCORE_ENVIRONMENT: Production` (§5) | Kritis | Sangat kecil |
| 4 | Guard environment untuk `seed demo` (§5d) | Kritis | Sangat kecil |
| 4b | HMAC/tanda tangan pada cookie sesi lite — guard tak bisa dipalsukan (§11b.1) | Tinggi | Kecil |
| 5 | Sufiks acak pada slug portofolio (§11.2) | Tinggi | Sangat kecil |
| 6 | `UseForwardedHeaders` + cookie `Secure` (§6) | Tinggi | Kecil |
| 7 | Test enumerasi `EndpointDataSource` sbg penjaga isolasi (§11.1) | Tinggi | Kecil |
| 8 | DataProtection persisten + sertifikat X.509 (§7) | Tinggi | Menengah |
| 9 | Filter tenant jadi *fail-closed* + opt-in eksplisit (§11.1) | Tinggi | Menengah |
| 10 | `StatusCodePages` untuk error JSON seragam (§8) | Menengah | Kecil |
| 11 | `robots.txt` — keputusan indeks portofolio siswa (§9a, §11.2) | Menengah | Sangat kecil |
| 12 | Commit pekerjaan yang menggantung (§9d) | Menengah | Kecil |
| 13 | `poweredByHeader: false`, ketatkan CSP produksi (§9b/c) | Rendah | Sangat kecil |
| 14 | Bedakan nav/halaman admin vs guru — guru tak lihat Billing (§11c.1) | Tinggi | Kecil |
| 15 | Sidebar `sticky top-0 h-screen` — tombol Keluar selalu terlihat (§11c.2) | Menengah | Sangat kecil |
| 16 | Panel notifikasi sadar-viewport, tak meluber (§11c.3) | Menengah | Sangat kecil |
| 17 | Perluas seed: invoice/rubrik/assessment/kunjungan + periode Assessment (§11c.4) | Menengah | Kecil |
| 18 | Tes terkontrol logout + pertimbangkan revoke-semua-sesi (§11c.5) | TBD | Kecil |

Dua catatan urutan:

- **#5 dan #11 sebaiknya dikerjakan bersamaan** — keduanya menyangkut privasi portofolio siswa, dan memperbaiki salah satunya saja menyisakan celah yang lain terbuka.
- **#7 lebih mendesak daripada #9** meski keduanya soal isolasi tenant. #9 memperbaiki arsitekturnya (lebih benar, tapi lebih berisiko regresi), sementara #7 hanya menambah satu test yang langsung menangkap seluruh endpoint sekarang **dan** yang akan datang. Pasang jaring pengamannya dulu, baru ubah fondasinya.

**Empat dari lima temuan kritis (#1, #3, #4, dan sebagian #5) diperbaiki hanya dengan menyunting konfigurasi dan runbook — bukan kode aplikasi.** Rasio dampak terhadap usaha di situ sangat tinggi; kerjakan lebih dulu.

---

## 11. Analisis akar — lapisan domain, kriptografi, dan isolasi data

Bagian ini menembus di bawah perimeter: logika bisnis, pembangkitan token, dan arsitektur isolasi tenant. Dua temuan akar baru, dan sejumlah hal yang saya periksa justru terbukti sehat.

### 11.1 AKAR — Filter isolasi tenant bersifat *fail-open*

**Tingkat: tinggi (risiko struktural, bukan kerentanan aktif).**

Seluruh 21 filter global di `VokasiaDbContext.ApplyTenantQueryFilters()` memakai pola yang sama:

```csharp
x => !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId
```

Bacaan literalnya: **"kalau tidak ada konteks tenant, tampilkan seluruh tenant."** Filter menonaktifkan dirinya sendiri saat konteks kosong — gagal ke arah terbuka, bukan tertutup.

Ini bukan kecelakaan; ini memang diperlukan, karena tiga jalur sah memang lintas-tenant: mentor industri (`TenantId = null` secara desain), SuperAdmin, dan cron worker. Kodenya pun mendokumentasikan ini secara terbuka di beberapa tempat.

Konsekuensinya: **isolasi data tidak lagi dijamin oleh filter, melainkan oleh kedisiplinan tiap endpoint menyaring manual.** Saat ini ada **12 endpoint** yang memakai `RequireAuthorization()` polos tanpa policy ber-tenant, sehingga sepenuhnya bergantung pada penyaringan manual di dalam handler-nya.

Saya memeriksa **kedua belas endpoint itu satu per satu**, dan hasilnya melegakan — semuanya menyaring dengan benar:

| Endpoint | Mekanisme penyaring |
|---|---|
| `GET /api/journals/pending` | filter `mentorId` dari klaim `sub` |
| `POST /api/journals/{id}/approve` · `/reject` · `/batch-approve` | `PlacementScopeHandler` (resource-based) |
| `GET /api/mentors/assessment-queue` | `p.MentorUserId == tenant.UserId` |
| `POST /api/assessments/{id}/mentor-scores` | `AuthorizeAsync(..., MentorOwnPlacement)` |
| `GET /api/assessments/{id}` | cek mentor resource + tolak non-mentor tanpa tenant |
| `GET /api/placements/{id}/certificate` | cek peran + `s.UserId == tenant.UserId` |
| `GET/POST /api/notifications*` | `n.UserId == userId` |
| `POST /api/audit` | actor diambil dari klaim `sub`, **bukan** dari body |
| `POST /api/impersonation/end` | butuh `ImpersonatorUserId` aktif |

**Jadi tidak ada kebocoran hari ini.** Masalahnya ada di lapisan berikutnya: **tidak ada apa pun yang menjaga agar hal itu tetap benar besok.**

```
[Fact] di TenantIsolationTests           : 8
URL yang benar-benar diuji isolasinya    : 3  (/api/audit, /api/journals/pending, /api/students)
Endpoint terdaftar di aplikasi           : 82 (dari /openapi/v1.json)
Test yang memindai EndpointDataSource    : TIDAK ADA
```

Artinya: bila sesi pengembangan berikutnya menambah endpoint baru dengan `RequireAuthorization()` polos dan lupa menyaring manual, endpoint itu akan **diam-diam menyajikan data lintas sekolah** — tanpa exception, tanpa test yang gagal, tanpa peringatan apa pun. Untuk SaaS multi-tenant yang menyimpan data anak di bawah umur, mode kegagalan senyap seperti ini adalah kelas risiko yang paling berbahaya justru karena tidak terlihat.

**Perbaikan akar** (membalik default, bukan menambal satu per satu):

1. Ubah filter jadi *fail-closed* — saat konteks tenant kosong, kembalikan kosong, bukan segalanya.
2. Untuk tiga jalur lintas-tenant yang sah, pakai `.IgnoreQueryFilters()` **eksplisit** (polanya sudah dipakai dengan benar di `SaCompaniesEndpoints`) sehingga niat lintas-tenant terlihat di kode, bukan tersirat dari ketiadaan klaim.
3. Tambahkan satu test yang mengenumerasi `EndpointDataSource` dan menggagalkan build bila ada endpoint tanpa policy ber-tenant yang tidak terdaftar di daftar-putih lintas-tenant. Satu test ini menggantikan kewajiban mengingat, untuk semua endpoint yang akan datang.

Langkah 3 saja sudah menutup risikonya secara permanen dengan biaya paling kecil.

### 11.2 AKAR — Slug portofolio dapat ditebak, meruntuhkan model privasi opt-in

**Tingkat: tinggi (privasi, menyangkut anak di bawah umur).**

`PortfolioEndpoints.GenerateUniqueSlugAsync` membentuk slug sepenuhnya dari data yang dapat diketahui publik:

```csharp
var baseSlug = Slugify($"{fullName}-{majorName}-{year}");
// tabrakan -> "-2", "-3", dst.
```

`Slugify` hanya menurunkan huruf ke kecil dan mengganti non-alfanumerik dengan tanda hubung. **Tidak ada komponen acak sama sekali.** Hasilnya berbentuk:

```
budi-santoso-rekayasa-perangkat-lunak-2026
```

Model privasi yang dirancang PRD adalah **opt-in**: siswa memilih mempublikasikan, lalu membagikan tautannya sendiri. Slug yang dapat ditebak meruntuhkan model itu — siapa pun yang memegang daftar nama siswa (pengumuman kelulusan, daftar kelas, akun media sosial sekolah — semuanya lazim publik) dapat menyusun slug secara massal, lalu memeriksa siapa saja yang punya portofolio terbit dan membaca isinya. Tidak perlu tautan dari siswa mana pun.

Dua faktor memperbesar dampaknya:

- **Tidak ada `robots.txt`** (terverifikasi 404 di §9a) — mesin pencari bebas mengindeks seluruh `/p/{slug}`.
- Subjek datanya adalah **siswa SMK**, sebagian di bawah umur, dan PRD proyek ini sendiri menyebut kepatuhan UU PDP.

Perlu ditegaskan agar adil: **isi DTO-nya sudah benar.** `PublicPortfolioDto` tidak memuat NISN maupun kontak, dan proyek ini bahkan memasang penjaga berbasis refleksi yang membuat `PublishPortfolio` **gagal keras** bila kelak ada field sensitif ditambahkan ke DTO itu — desain defensif yang bagus dan jarang saya temui. Kelemahannya bukan pada isi, melainkan pada **URL-nya sendiri**: alamatnya membawa nama lengkap dan dapat diterka.

**Perbaikan**: tambahkan sufiks acak pendek pada slug (mis. 8 karakter base62 dari CSPRNG) — `budi-santoso-rpl-2026-k7m2xq9p`. URL tetap terbaca manusia, tapi tidak lagi dapat dienumerasi. Sekaligus putuskan secara sadar isi `robots.txt` untuk `/p/`.

### 11.3 Diperiksa sampai akar, dan terbukti sehat

Empat hal ini saya bongkar karena berpotensi jadi kesalahan diam-diam yang mahal. Semuanya lolos:

**Matematika nilai berbobot — benar.** `AssessmentScoring.ComputeWeightedScore` menghitung `Σ(nilai × bobot) / 100`. Rumus itu hanya sahih bila total bobot tepat 100 — kalau tidak, seluruh nilai siswa akan salah secara senyap. Saya lacak apakah ada yang menegakkannya, dan ada: `RubricEndpoints.WeightsSumTo100` divalidasi **baik saat membuat maupun saat memperbarui** rubrik, menolak dengan 422 bila tidak tepat 100. Pembulatan juga memakai `MidpointRounding.AwayFromZero`, bukan banker's rounding — pilihan yang tepat untuk nilai akademik. Aspek yang belum dinilai melempar `IncompleteScoresException`, tidak diam-diam dianggap nol.

**Token magic link mentor — kuat.** 32 byte dari `RandomNumberGenerator` (CSPRNG) → base64url; disimpan sebagai hash SHA-256, bukan mentah; sekali pakai lewat `UsedAt`; TTL 72 jam; dan pesan galat **identik** untuk kasus tidak-ditemukan/sudah-dipakai/kedaluwarsa sehingga tidak menjadi oracle. Penukaran token juga menolak email yang sudah terdaftar dengan peran lain, mencegah kebingungan hak akses.

**Kode sertifikat — memadai untuk perannya.** 12 karakter alfanumerik dari CSPRNG (62¹², bukan sekuensial). Bias modulo diakui terbuka di komentar dan memang dapat diterima karena kode ini bersifat tampilan, bukan rahasia.

**Permukaan query — bersih.** Nol penggunaan raw SQL/`FromSql` di seluruh backend, sehingga tidak ada jalur yang melewati filter EF. `IgnoreQueryFilters()` hanya muncul di satu tempat (`SaCompaniesEndpoints.MergeCompanies`, kebijakan `SaOnly`, terdokumentasi) — bukan tersebar.

---

## 11-bis. Uji serangan langsung di browser (sesi ini)

Dilakukan live di Chrome + curl. Setiap baris di bawah adalah hasil perintah yang saya jalankan sekarang, bukan pembacaan kode.

### 11b.1 TEMUAN TINGGI — Cookie sesi "lite" tanpa tanda tangan, guard UI bisa dilewati

**Tingkat: tinggi (kontrol keamanan rusak + pertahanan berlapis bolong). BUKAN kebocoran data.**

Cookie `vok_session` yang dibaca `proxy.ts` untuk menentukan boleh-tidaknya membuka `/app` `/sa` `/mentor` `/student` hanyalah **base64url dari JSON, tanpa HMAC/tanda tangan** (`lib/session.ts:69` `encodeSessionCookie`). Artinya siapa pun bisa mengarangnya.

Saya buktikan langsung. Kontrol — tanpa cookie, guard bekerja benar:

```
/sa      -> 200, dialihkan ke /login?error=unauthenticated&next=%2Fsa
/app     -> 200, dialihkan ke /login...
/student -> 200, dialihkan ke /login...
/mentor  -> 200, dialihkan ke /login...
```

Lalu dengan cookie `vok_session` palsu (base64 JSON `{role:"SuperAdmin"}`, dibuat dalam 1 baris Python, tanpa login):

```
SuperAdmin     /sa      -> 200, TETAP di /sa       (guard dilewati)
TenantAdmin    /app     -> 200, TETAP di /app      (guard dilewati)
Student        /student -> 200, TETAP di /student  (guard dilewati)
IndustryMentor /mentor  -> 200, TETAP di /mentor   (guard dilewati)
```

Shell panel SuperAdmin benar-benar ter-render — navigasi lengkap "Ringkasan · Tenant · DUDI · Paket · Invoice · Audit · Keluar" muncul di HTML (31 KB).

**Yang menahannya menjadi bencana** (dan alasan ini "tinggi", bukan "kritis"): lapisan data tetap rapat. Semua panggilan lewat BFF proxy dengan cookie palsu itu **tetap 401**, karena cookie sesi asli yang menyimpan access token (`vok_bff_sess`) tidak ada:

```
GET /api/proxy/sa/tenants  (cookie lite palsu)  -> 401 {"message":"Belum login."}
GET /api/auth/session       (cookie lite palsu)  -> 401 {"user":null}
```

Di shell SA yang ter-render pun, area data berbunyi *"KPI & system health belum bisa dimuat"* — nol data asli. **Cross-role juga benar**: cookie ber-peran Student yang mencoba `/sa` dialihkan ke `/student`, bukan menembus shell SA.

**Jadi dampak sebenarnya, tepatnya:**
- Penyerang tak terautentikasi bisa **merender kerangka UI peran apa pun, termasuk SuperAdmin**, tanpa login — membocorkan struktur navigasi, nama fitur, tata letak rute internal. Info-disclosure ringan.
- **Tidak ada data yang bocor** hari ini — API menegakkan token asli secara independen.
- Tapi ini **kontrol keamanan yang bisa dilewati sepele**, dan pertahanan berlapisnya bolong: begitu ada satu halaman masa depan yang merender data sensitif server-side berdasarkan peran dari cookie lite ini (bukan lewat proxy ber-token), ia langsung berubah jadi kebocoran nyata.

Yang menarik — dan patut disebut demi keadilan: **kode proyek ini sudah menyadarinya.** Komentar di `lib/session.ts:17-21` menulis eksplisit bahwa cookie ini "BUKAN batas keamanan" dan merekomendasikan menandatanganinya dengan HMAC "sebagai pertahanan berlapis — dicatat sebagai saran, bukan blocker". Temuan ini menaikkan saran lama itu menjadi rekomendasi rilis: untuk produksi, tanda tangani cookie (HMAC dengan kunci dari secret store) sehingga guard tidak bisa dipalsukan.

### 11b.2 Uji yang LULUS (aktif dicoba dirusak, bertahan)

- **`/verify` submit kosong** → validasi HTML5 `required` menahan ("Harap isi bidang ini"), tidak ada request terkirim.
- **`/verify` spasi saja** → tidak menghasilkan halaman hasil (input ter-trim), tetap di form.
- **`/verify` kode 200 karakter** → hasil "tidak ditemukan", teks membungkus rapi, **tidak ada** kerusakan layout.
- **`/verify` payload `<script>`** → tampil sebagai teks apa adanya, URL ter-encode (`%3Cscript%3E`), tidak dieksekusi.
- **`/mentor-invite` tanpa token** → "Undangan belum bisa digunakan · Tautan tidak lengkap".
- **`/mentor-invite?token=palsu`** → "Tautan tidak valid atau sudah kedaluwarsa" (pesan berbeda dari kasus tanpa-token — pembedaan yang tepat).
- **Open redirect via `/api/auth/login?next=...`** → dicoba 5 payload jahat (`https://evil`, `//evil`, tab-injection `%09//evil`, `javascript:`); **semua** menghasilkan `redirect_uri` yang tetap `http://localhost:3000/api/auth/callback` — parameter jahat tidak pernah menyentuh redirect. `getSafeLocalReturnUrl` (`login/route.ts:30`) menyaringnya, dan `next` disimpan di state PKCE server-side (Redis, sekali-pakai, ber-TTL), bukan dipantulkan.
- **Callback OAuth palsu** (`?code=palsu&state=ngawur`) → ditolak, dialihkan ke login. `consumePkce` menolak state tak dikenal (anti-CSRF + anti-replay).
- **Halaman `/offline`** → ter-render benar dengan tombol "Coba lagi".

## 11-ter. Temuan area terautentikasi (dilaporkan user saat login live, diverifikasi ke kode + DB)

User login sungguhan sebagai TenantAdmin dan melaporkan sejumlah masalah. Saya verifikasi tiap satu ke kode sumber dan/atau database — bukan menerima begitu saja.

### 11c.1 TEMUAN TINGGI — Admin sekolah & guru mendapat halaman + navigasi identik

**Verifikasi: `lib/roleHome.ts` + `components/SidebarNav.tsx`.**

`roleHome` memetakan **tiga peran berbeda ke tujuan yang sama**:

```
TenantAdmin -> /app   DeptHead -> /app   Teacher -> /app
```

Dan `SidebarNav` hanya satu array menu yang di-hardcode, **tanpa cabang peran**:

```
Ringkasan · Bimbingan · Penilaian · Billing
```

Akibatnya guru pembimbing melihat menu identik dengan admin sekolah — **termasuk "Billing"**, yang murni urusan admin (tagihan langganan sekolah). Peran keduanya berbeda secara fundamental: admin mengelola satu sekolah penuh; guru hanya membimbing murid yang ditugaskan padanya. Isolasi data-nya memang ditegakkan di API (policy `TeacherPlus` dst.), tapi **UI tidak membedakan sama sekali** — guru mendapat kerangka admin. Ini kebingungan peran di lapisan UI + kebocoran menu fungsi yang tak relevan.

**Perbaikan**: cabangkan `SidebarNav` per peran (guru: Bimbingan + Penilaian saja; admin: + Billing + kelola sekolah), dan pertimbangkan rute/di-scope berbeda untuk pengalaman guru vs admin.

**Konfirmasi live (dead-end menu "Billing" untuk guru).** Diamati langsung di browser saat login sebagai guru: klik menu "Billing" → halaman menampilkan **kotak error MERAH** "Billing belum bisa dimuat (mungkin kamu tidak punya akses — hanya Admin Sekolah)". Akarnya terbukti di kode: `app/(school)/app/billing/page.tsx` memanggil `fetcher("/invoices")`, tapi backend `/api/invoices` ber-policy **`TenantAdminOnly`** (lebih sempit dari route guard yang mengizinkan Teacher/DeptHead masuk `/app`) → **403** → ditangkap `try/catch` → render `ErrorState` merah. Komentar di `page.tsx:11-12` **sudah menyadari** perilaku ini ("DeptHead/Teacher yang membuka /app/billing akan dapat 403") tapi menu-nya tetap ditampilkan. Jadi guru dan kepala jurusan mendapat **menu yang SELALU gagal** — keamanan API benar (403 tepat), tapi UX terbalik: menu yang pasti ditolak tak seharusnya muncul. Menyembunyikan item "Billing" untuk non-admin menutup ini sekaligus (fix yang sama dengan §11c.1).

**Tur guru live — empat halaman diamati langsung (sesi guru "Utama Damanik"):**

| Halaman | Hasil | Catatan |
|---|---|---|
| **Ringkasan** | Data kaya, jalan | Menampilkan **"26 Siswa Bermasalah" lintas SELURUH sekolah** (banyak DUDI berbeda), bukan scope guru. `GetSchoolDashboard` ber-policy `TenantMember` tanpa filter guru → guru dapat dashboard admin. Landing guru semestinya view ter-scope, bukan monitoring sekolah penuh. |
| **Bimbingan** | Jalan, **scope benar** ✓ | "Siswa yang di-assign kepadamu sebagai guru pembimbing" — ~65 murid, riwayat jurnal + status RAG + kotak komentar. Inilah view guru yang benar. |
| **Penilaian** | Empty-state **anggun** ✓ | Abu-abu, menjelaskan "Belum ada periode fase penilaian... aktif saat periode masuk fase penilaian" + tombol "Periksa lagi". Kosong karena semua periode "Active" (bukan "Assessment") — UX benar. |
| **Billing** | Error **MERAH** ✗ | 403 dead-end (lihat atas). |

**Kontras UX yang mengajarkan (Penilaian vs Billing).** Keduanya "tak menampilkan isi", tapi Penilaian pakai empty-state abu-abu yang menjelaskan & menenangkan, sedangkan Billing pakai error merah yang mengkhawatirkan. Bedanya tepat: Penilaian kosong karena **data** (wajar, sementara), Billing merah karena **akses** (guru tak seharusnya di sana sama sekali). Pelajarannya: "tak ada akses" bukanlah "error" — dan paling benar diselesaikan dengan tidak menampilkan menunya, bukan dengan mewarnainya merah.

**Kesimpulan tur guru**: fitur & data guru sebenarnya tampil baik (Bimbingan kaya, Penilaian empty-state benar) — dua masalah UX-nya adalah **scope** (Ringkasan sekolah-penuh untuk guru) dan **menu dead-end** (Billing merah), keduanya berakar pada satu hal: navigasi + landing guru tidak dibedakan dari admin (§11c.1).

### 11c.2 TEMUAN MENENGAH — Sidebar tidak sticky, tombol "Keluar" terdorong ke dasar konten

**Verifikasi: `app/(school)/app/layout.tsx`.**

`<aside>` desktop memakai `mt-auto` (flexbox) untuk menaruh `LogoutButton` di bawah, tapi **tanpa `sticky`/`fixed`/`h-screen`**. Sidebar ikut tinggi kontainer flex yang mengikuti panjang konten. Di halaman dengan konten panjang, "Keluar" tersorong ke dasar seluruh konten — user harus scroll jauh ke bawah untuk menemukannya. Persis yang dialami user.

**Perbaikan**: `sticky top-0 h-screen` (atau `h-dvh`) pada `<aside>` sehingga sidebar + tombol Keluar tetap terlihat terlepas dari panjang konten.

### 11c.3 TEMUAN MENENGAH — Popup notifikasi meluber ke arah salah

**Verifikasi: `components/NotificationPanel.tsx:63` + penempatan lonceng di `app/(school)/app/layout.tsx`.**

Panel memakai posisi yang di-hardcode:

```
absolute right-0 top-full ... w-[min(20rem,calc(100vw-2rem))]
```

`right-0` meng-anchor tepi kanan panel ke lonceng. Tapi di layout `/app` desktop, lonceng ada di **sidebar kiri** (~248px dari tepi kiri). Panel selebar 320px yang ter-anchor di situ menghitung tepi kirinya ke sekitar **−70px — meluber keluar tepi kiri viewport**. Satu nilai `right-0` yang di-hardcode tidak bisa benar untuk dua penempatan lonceng sekaligus: lonceng-kiri (sidebar desktop) butuh buka ke kanan, lonceng-kanan (header mobile, topbar SA) butuh buka ke kiri. User mengonfirmasi arah popup terasa salah.

**Perbaikan**: buat panel sadar-viewport — anchor `left-0` saat lonceng di sisi kiri (sidebar desktop), `right-0` saat lonceng di kanan; atau hitung posisi agar tak pernah meluber keluar layar.

### 11c.4 TEMUAN MENENGAH — Bimbingan / Penilaian / Billing kosong (akar: data seed tidak lengkap)

**Verifikasi: query langsung ke DB `vokasia`.**

```
Invoices (billing)        : 0
Assessments (penilaian)   : 0
RubricTemplates           : 0
Visits (kunjungan)        : 0
TeacherComments (bimbingan): 0
Status periode            : kelima periode = "Active" (tak ada yang "Assessment")
```

Fiturnya ada, tapi tak ada data untuk ditampilkan: Billing kosong (nol invoice), Penilaian tak bisa terisi (fase penilaian tak pernah dibuka — semua periode masih "Active", bukan "Assessment"), Bimbingan tanpa kunjungan/komentar. Ini melebar dari temuan §0 sebelumnya: **`seed demo` menghasilkan siswa + jurnal, tapi bukan invoice, rubrik, assessment, kunjungan, atau komentar** — sehingga tiga dari empat tab admin tampak kosong saat pertama dibuka. Untuk QA/demo yang bermakna, seed perlu diperluas (atau sediakan tombol "isi data contoh").

### 11c.5 PENDING — Logout: kode benar, tapi 4 sesi menumpuk di Redis

**Verifikasi: `app/api/auth/logout/route.ts` + `lib/bffSession.ts` + inspeksi Redis langsung.**

Kode logout **terlihat benar**: `LogoutButton` adalah POST form → handler memanggil `deleteSession` (`redis.del sess:{id}`) + revoke refresh token + hapus cookie; kunci HMAC cookie sesi (`SESSION_SECRET`) **stabil** dari `.env`, jadi `decodeSessCookie` mestinya mengembalikan sessionId yang benar. Namun snapshot Redis menunjukkan **4 sesi `sess:*` menumpuk** (3 di antaranya "Admin SMK Negeri 1 Makmur").

Ini **belum saya vonis** sebagai bug: 4 sesi itu sama-sama konsisten dengan (a) logout gagal menghapus, ATAU (b) user login berkali-kali tanpa klik Keluar (yang memang terjadi selama sesi audit ini). Menunggu **satu tes terkontrol**: snapshot ID sesi diambil → user klik "Keluar" sekali → cek ulang apakah sesi itu hilang. Sampai tes itu jalan, status: **belum terkonfirmasi**.

*(Catatan terpisah yang memang benar terlepas dari hasil tes: logout hanya menghapus sesi SAAT INI, bukan seluruh sesi user di perangkat/tab lain — indeks `user-sessions:` dibangun tapi tak dipakai untuk revoke-semua; dan access-token JWT tetap sah ~15 menit pasca-logout karena stateless. Keduanya lazim, tapi layak dicatat untuk konteks sekolah dengan perangkat bersama.)*

## 11-quater. Root-cause menyeluruh `/sa` (SuperAdmin) — via kode + DB

Karena login tak bisa saya lakukan sendiri (classifier memblokir password DAN magic-link), seluruh `/sa` saya bedah lewat kode sumber + query DB langsung — metode yang sama yang menemukan temuan `/app`.

### 11d.1 Akar tunggal: seed demo hanya mengisi "core loop", bukan lapisan komersial/ops

Query DB langsung atas keenam halaman SA:

```
/sa/tenants  -> 3    (ADA)      /sa/plans    -> 0  (KOSONG)
/sa/dudi     -> 102  (ADA)      /sa/invoices -> 0  (KOSONG)
                                /sa/audit    -> 0  (KOSONG)
KPI MRR      -> Rp 0            FeatureFlags -> 0  (KOSONG)
```

Penyebabnya satu dan sama dengan `/app`: `DemoSeeder` membuat tenant, DUDI, siswa, dan jurnal — tapi **tidak** membuat Plan, Invoice, FeatureFlag, atau AuditLog. Akibatnya:
- **3 dari 6 halaman SA kosong** (Paket, Invoice, Audit).
- **KPI "MRR" selalu Rp 0** — karena MRR dihitung `Σ harga plan tenant aktif` (`SaOpsEndpoints.GetPlatformKpis`), tapi tak ada Plan dan tak ada tenant yang punya `PlanId`.

Fitur & endpoint-nya benar — pagesnya render, query-nya jalan. Yang tak ada murni **data**. Ini bukan bug kode; ini kelengkapan seed.

**Yang sudah saya lakukan** (agar `/sa` bisa dilihat berisi saat login): membuat 2 Plan (Dasar Rp 500rb, Pro Rp 1,5jt), menetapkannya ke 3 tenant, membuat 6 invoice (status campur Issued/ProofUploaded untuk menguji alur konfirmasi bayar), dan feature flag tingkat-plan. **MRR kini Rp 2.500.000**, dan 5 dari 6 halaman SA berisi. `/sa/audit` sengaja dibiarkan kosong — audit log HANYA ditulis oleh aksi in-app nyata (impersonasi, verify DUDI, dst.), jadi kosong sampai SA benar-benar beraksi, dan itu perilaku yang benar.

### 11d.2 Bug UI sistemik ikut hadir di `/sa`

`app/(sa)/sa/layout.tsx` memakai pola layout yang **identik** dengan sekolah — jadi dua bug ini menjangkiti `/sa` juga:
- **Sidebar tak sticky** (§11c.2) — tombol "Keluar" terdorong ke dasar konten panjang.
- **Lonceng di sidebar kiri + panel `right-0`** (§11c.3) — panel notifikasi meluber keluar tepi kiri.

Karena ketiga workspace (school, sa, mentor/student) berbagi pola layout yang sama, **satu perbaikan sticky + satu perbaikan panel notifikasi menutup ketiganya sekaligus** — bukan tiga tambalan terpisah.

### 11d.3 Kerapuhan dashboard SA — gagal satu, blank semua

`app/(sa)/sa/page.tsx` memuat `Promise.all([fetcher("/sa/kpis"), fetcher("/sa/health")])` dalam satu `try/catch`. Kalau **salah satu** endpoint gagal, SELURUH dashboard jadi ErrorState "belum bisa dimuat" — bukan menampilkan bagian yang berhasil. (`/sa/health` sendiri dirancang best-effort mengembalikan null, jadi jarang throw; tapi pola all-or-nothing ini rapuh.) Bukan bug kritis, tapi degradasi yang tidak anggun. **Catatan penting**: "belum bisa dimuat" yang sempat terlihat di audit sebelumnya adalah artefak cookie palsu tanpa token (API 401) — **bukan** bug dashboard; dengan sesi SA asli, halaman termuat.

## 11-quinquies. Shell mentor & siswa dipaksa mobile-only (bukan responsif)

**Verifikasi: `app/(mentor)/mentor/layout.tsx` + `app/(student)/student/layout.tsx`.**

Kedua shell membungkus konten dengan **batas keras lebar-HP** dan bottom-nav yang tak pernah disembunyikan:

```
<div class="mx-auto flex min-h-screen max-w-lg flex-col border-x ...">   // 512px, dikunci
...
<RoleMobileNav items={NAV} />   // TANPA hideAtDesktop → bottom nav muncul juga di desktop
```

Akibatnya di layar desktop: kolom sempit selebar ponsel di tengah layar yang mayoritas kosong, plus bilah navigasi bawah gaya HP yang tetap menempel. Ini **mobile-only**, bukan mobile-first-yang-responsif.

Kontras dengan shell sekolah/SA yang **sudah responsif benar**: `max-w-[1600px]`, sidebar desktop `lg:flex`, dan `RoleMobileNav ... hideAtDesktop` (bottom nav hanya di mobile). Jadi polanya sudah ada di repo — tinggal diterapkan ke mentor/siswa.

**Catatan change-control**: `DESIGN.md` (beku, D20) menyebut `/student` `/mentor` "mobile-first (Android murah, 3G, 360px)". Membuatnya responsif-ke-desktop adalah **perubahan sadar atas keputusan beku itu** — layak dicatat sebagai entry DECISIONS baru, bukan diam-diam. Mobile-first tetap terjaga (mobile tetap prioritas & ringan); yang berubah hanya: desktop tak lagi dikunci ke kolom HP.

**Perbaikan — arah dipilih Developer: sidebar kiri seperti `/app`** (bukan sekadar kolom lebih lebar). Terapkan pola `app/(school)/app/layout.tsx` ke shell mentor & siswa:
- Ganti pembungkus `max-w-lg` dengan struktur responsif `max-w-[1600px]` + `<aside className="hidden ... lg:flex">` berisi `WorkspaceSidebar` (item NAV mentor/siswa yang sudah ada + `LogoutButton` di `mt-auto`).
- Pindahkan `NotificationBell`/`LogoutButton` ke sidebar desktop; pertahankan header ringkas untuk mobile (`lg:hidden`).
- Tambahkan `hideAtDesktop` pada `RoleMobileNav` → bottom-nav hanya muncul di mobile.
- **Sekalian tutup dua bug sistemik** (§11c.2, §11c.3): buat `<aside>` `sticky top-0 h-screen` (tombol Keluar selalu terlihat) dan perbaiki anchor panel notifikasi agar tak meluber. Satu penataan ulang shell mentor/siswa mengikuti pola `/app` menuntaskan responsif + sticky + notifikasi sekaligus.

Halaman di dalamnya (JournalForm, ApprovalCard, dst.) tetap dipakai apa adanya — cukup shell-nya. Mobile tetap satu kolom + bottom-nav, mobile-first terjaga.

**Status: rekomendasi (belum diimplementasikan)** — atas permintaan Developer, dicatat di audit dulu; eksekusi menyusul, dengan entry DECISIONS baru sebagai change-control atas keputusan mobile-first beku (D20).

## 12. Yang masih perlu diverifikasi terpisah

Bukan temuan — pekerjaan verifikasi yang belum terlaksana, dan **tidak boleh dianggap lulus** hanya karena tidak muncul sebagai temuan:

- Seluruh area terautentikasi: `/app`, `/sa`, `/mentor`, `/student` — fungsionalitas, kelengkapan data, dan UX-nya.
- Jalur sukses `/verify/{kode-valid}` dan `/p/{slug-terpublish}`.
- `dotnet test` (klaim 330 lulus), `bun test` (klaim 57), dan `next build` produksi.
- Ukuran bundle `/student` < 200KB. Saya mencoba mengukurnya lewat header respons, tapi Next.js memakai `Transfer-Encoding: chunked` tanpa `Content-Length` sehingga angkanya tidak dapat dipercaya — **tidak saya laporkan** daripada menyajikan angka yang salah.
- Lighthouse mobile, instalasi PWA di perangkat Android sungguhan, E2E lima persona.
- Perilaku rate limit **setelah** ForwardedHeaders dipasang (§6) — wajib diuji ulang, karena perbaikan itu mengubah dasar partisinya.

---

## 13. Pass kesiapan-produksi (pasca-fix v3) — bug baru & verifikasi fix

Pass baru setelah seluruh fix v3 diterapkan. Fokus: area yang belum pernah dibedah (otorisasi tingkat-objek, konkurensi, lapisan worker) + verifikasi fix baru benar-benar jalan. Semua dari pembacaan kode + probe runtime sesi ini.

### 13.1 Fix v3 yang terverifikasi KOKOH (bukan sekadar klaim)

- **`vok_session` kini ditandatangani** — probe live: cookie lite palsu `{role:"SuperAdmin"}` → `/sa` **redirect ke login** (`access_required`); sebelumnya render shell SA. Forgery ditutup. ✓
- **RFC7807** — `GET /api/rute-ngawur` → `404` `application/problem+json` body `{type,title,status,instance}`. ✓
- **ForwardedHeaders aman** — `ForwardedHeadersSetup.cs`: `ForwardLimit=1`, `KnownProxies`/`KnownIPNetworks` wajib eksplisit, default hanya loopback. Jadi `X-Forwarded-For` **tidak** bisa dipalsukan klien luar → rate-limiter IP anti-spraying tak bisa dilewati lewat header. Fix diterapkan dengan benar (bukan `ForwardedHeaders.All` yang justru akan membuka bypass). ✓
- **Presign upload aman** — object key **di-generate server** (`tenant/{tenantId}/journal/{guid}`), klien hanya kirim ContentType (whitelist). Tak ada path traversal/overwrite. ✓
- **Core write endpoints** — `SubmitJournal` cek slot milik placement siswa; `JournalEntry.SlotId` **unique index** (race dobel-submit → DbUpdateException, bukan dobel baris); `AttachPhoto` cek entry milik siswa + `EnsureMutable`. ✓

### 13.2 TEMUAN BARU (TINGGI) — Otorisasi tingkat-objek storage bocor (IDOR reference-injection)

**Ini temuan utama pass ini. Belum pernah muncul di audit mana pun.**

Semua endpoint **presign** benar (key di-generate server, ter-namespace `tenant/{tid}/...`). Tapi endpoint **"attach/save"** pasangannya **mempercayai object key dari klien tanpa memvalidasi bahwa key itu milik namespace tenant si pemanggil**. Terkonfirmasi di tiga tempat:

| Endpoint | Baris | Field klien | Validasi prefix? |
|---|---|---|---|
| `AttachPhoto` (jurnal) | `Dtos.cs:60` `AttachPhotoRequest(string ObjectKey)` | `req.ObjectKey` | **TIDAK ADA** (grep validator: nihil) |
| `CreateVisit` | `VisitEndpoints.cs:86` | `req.PhotoKey` | **TIDAK ADA** |
| `UploadPaymentProof` | `BillingEndpoints.cs:104` | `req.ObjectKey` → `invoice.ProofKey` | **TIDAK ADA** |

Grep seluruh `Vokasia.Api` untuk validasi prefix `tenant/` pada ObjectKey → **nol hasil**.

**Rantai dampak (jalur jurnal, paling parah karena berujung ke halaman PUBLIK):**
1. Siswa panggil `AttachPhoto` pada jurnalnya sendiri, tapi kirim `ObjectKey` = key objek **sembarang di bucket** (mis. `tenant/{tenant-lain}/journal/{guid}.jpg`, atau `cert/....pdf`, atau objek tenant lain).
2. `AttachPhoto` hanya cek entry milik siswa — **tidak** cek object key. Tersimpan apa adanya.
3. Worker `PhotoUploadedConsumer.cs:69` `GetObjectAsync(WithObject(photo.ObjectKey))` — **download + proses key mentah**, tanpa cek prefix.
4. Saat jurnal disetujui & portofolio dipublish, `PortfolioEndpoints.cs:247` `PresignedGetObjectAsync(WithObject(key))` atas key itu → **objek ditampilkan di `/p/{slug}` yang PUBLIK**.

**Artinya:** invarian "object key selalu milik tenant pemilik" **diasumsikan tapi tak pernah ditegakkan**. Seorang siswa bisa membuat portofolio publiknya menampilkan **objek lintas-tenant apa pun yang key-nya ia ketahui** — foto siswa sekolah lain, PDF sertifikat, dsb. Worker juga memproses key sembarang (vektor abuse/error).

**Penilaian severity (jujur):** eksploitasi tertarget butuh **tahu key korban**; key jurnal = dua-GUID (sulit ditebak) → bukan pencurian massal sepele. Tapi ini **broken object-level authorization (OWASP A01/IDOR)** yang nyata: key yang bocor (log, pesan error, respons sebelumnya), pola key non-tenant-scoped (sertifikat), atau objek milik sendiri lintas-entry semuanya jadi bisa disalahreferensikan — dan muara-nya halaman publik yang memuat data anak di bawah umur. Security review pasti menandai ini.

**Perbaikan** (kecil, satu tempat): validasi setiap object key dari klien harus berawalan `tenant/{callerTenantId}/{prefix-yang-diharapkan}/` sebelum disimpan — validator bersama untuk ketiga endpoint. Idealnya juga: worker & portofolio menolak key di luar prefix tenant sebagai pertahanan berlapis.

### 13.3 TEMUAN BARU (RENDAH) — Finalisasi penilaian: check-then-act tanpa concurrency token

`FinalizeAssessment` menjaga idempotensi dengan `if (assessment is { IsFinal: true }) continue;` — baik. Tapi ini pola **baca-lalu-tulis** tanpa optimistic-concurrency token / kunci baris: dua request finalize bersamaan bisa sama-sama membaca `IsFinal=false` lalu dua-duanya menulis final. Dampak nyata terbatas (enqueue sertifikat lewat cron H+1 + idempotency consumer meredam efek ganda), jadi **rendah** — tapi untuk angka nilai akademik yang "terkunci permanen", menambahkan `[ConcurrencyCheck]`/`xmin` rowversion pada `Assessment.IsFinal` menutup celah teori ini rapi.

### 13.4 Ringkas prioritas pass ini

| # | Temuan | Tingkat | Effort |
|---|---|---|---|
| 13.2 | Validasi prefix-tenant object key di AttachPhoto/CreateVisit/UploadPaymentProof (+worker/portfolio) | Tinggi | Kecil |
| 13.3 | Concurrency token pada `Assessment.IsFinal` | Rendah | Sangat kecil |

Sisanya yang kokoh (§13.1) tidak butuh tindakan. Residual yang **kamu** sudah tandai (tenant-filter fail-open pada worker, Testcontainers npipe, NuGet advisories, E2E login) tetap berlaku dan tak saya ulang di sini.
