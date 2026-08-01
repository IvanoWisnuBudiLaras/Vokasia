# ALUR PROGRAM VOKASIA — Login → Role → Verifikasi Sertifikat

**Dibaca langsung dari kode**, 1 Agustus 2026. Setiap langkah menyebut file sumbernya
supaya kamu bisa membuka kodenya kalau ada yang bertanya di tengah presentasi.

---

## RINGKASAN SATU LAYAR

```
┌─ MASUK ────────────────────────────────────────────────────────────┐
│  Jalur A: staf & siswa  → OAuth2 Authorization Code + PKCE         │
│  Jalur B: mentor DUDI   → Magic link (grant kustom, tanpa PKCE)    │
└────────────────────────────────────────────────────────────────────┘
                              ↓
┌─ ROLE MENEMPEL DI 4 LAPIS ─────────────────────────────────────────┐
│  1. Klaim di JWT          sub · role · tenant_id · name            │
│  2. Cookie "lite"         dibaca guard TANPA panggil DB            │
│  3. Route guard frontend  segment /sa /app /mentor /student        │
│  4. Policy RBAC di API    + filter tenant otomatis di ORM          │
└────────────────────────────────────────────────────────────────────┘
                              ↓
┌─ VERIFIKASI SERTIFIKAT ────────────────────────────────────────────┐
│  Publik. Tanpa login. Tanpa tenant. Sengaja lintas semua sekolah.  │
└────────────────────────────────────────────────────────────────────┘
```

---

## BAGIAN 1 — LOGIN (Jalur A: staf sekolah & siswa)

### Alur lengkap, 9 langkah

| # | Terjadi di | Yang terjadi |
|---|---|---|
| 1 | Browser | Buka `/app`. Belum ada cookie sesi |
| 2 | `guard.ts` | Guard menolak → redirect `/login?error=access_required&next=/app` |
| 3 | `api/auth/login/route.ts` | Generate **PKCE verifier + challenge + state**, simpan di **Redis**, redirect ke `/connect/authorize` di API |
| 4 | API (OpenIddict) | Tampilkan form login. User isi email + password |
| 5 | API | Validasi via ASP.NET Identity → terbitkan **authorization code**, redirect balik ke `/api/auth/callback?code=...&state=...` |
| 6 | `api/auth/callback/route.ts` | **Konsumsi state dari Redis** (sekali pakai — anti CSRF & anti replay). Kalau state tidak dikenal/kedaluwarsa → tolak |
| 7 | BFF → API | `POST /connect/token` server-to-server, bawa `code` + `code_verifier` + `client_secret` |
| 8 | BFF | Terima `access_token` + `refresh_token`. **Simpan di Redis**, browser tidak pernah melihatnya |
| 9 | BFF | Set **2 cookie**, redirect ke `roleHome(role)` |

### Detail langkah 9 — kenapa dua cookie, bukan satu

Ini pertanyaan yang akan ditanya orang teknis. Jawabannya konkret:

| Cookie | Isi | Kenapa ada |
|---|---|---|
| `vok_sess` | ID sesi **opaque** → kunci ke Redis | Pemegang token asli. Tidak berarti apa-apa kalau dicuri tanpa akses Redis |
| `vok_session` | Klaim ringan: `id`, `name`, `role`, `tenantId` — **ditandatangani HMAC-SHA256** | Guard route harus tahu role **tanpa memanggil Redis/DB** di setiap navigasi |

> Kalau cuma satu cookie opaque, setiap perpindahan halaman harus menembak Redis dulu
> hanya untuk tahu "orang ini boleh masuk `/app` atau tidak". Cookie kedua menghapus
> round-trip itu. Isinya bukan rahasia — persis klaim yang toh sudah ada di JWT — tapi
> **ditandatangani** supaya tidak bisa dipalsukan.

**Yang tidak boleh dilupakan saat menjelaskan:** cookie kedua **bukan batas keamanan**.
Dia cuma penentu ke mana user diarahkan. Keamanan sesungguhnya ditegakkan di API, yang
memvalidasi access token asli secara independen. Kalau seseorang memalsukan cookie itu,
paling banter dia melihat kerangka halaman kosong — semua panggilan datanya tetap ditolak.

Kedua cookie: `httpOnly` · `secure` (produksi) · `sameSite=lax` · masa hidup 14 hari,
mengikuti umur refresh token.

**Sumber:** `frontend/src/lib/session.ts`, `frontend/src/lib/bffSession.ts`,
`frontend/src/app/api/auth/callback/route.ts`

---

## BAGIAN 2 — LOGIN (Jalur B: mentor industri, magic link)

Alur berbeda, dan **perbedaannya disengaja**.

| | Jalur A (staf/siswa) | Jalur B (mentor) |
|---|---|---|
| Grant type | `authorization_code` | `urn:vokasia:params:oauth:grant-type:magic-link` |
| PKCE | Wajib | Tidak ada |
| Password | Ada | **Tidak ada sama sekali** |
| Langkah user | Buka → login → isi form → masuk | **Klik link di email → masuk** |
| Masa berlaku | — | Token 72 jam, **sekali pakai** |

### Kenapa ada halaman perantara `/mentor-invite`

Link di email **tidak langsung** menuju endpoint penukar token. Dia menuju halaman
`/mentor-invite` dulu.

Alasannya teknis dan penting: banyak klien email dan antivirus **membuka setiap link
lebih dulu** untuk memindainya. Kalau link email langsung menukar token, pemindai email
akan menghabiskan token sekali-pakai itu sebelum mentor sempat mengkliknya — dan mentor
mendapat error "link sudah dipakai" padahal dia belum pernah membukanya.

Maka dipisah:

1. `/mentor-invite` memanggil **validate** — hanya mengecek, **tidak mengkonsumsi**
2. Halaman menampilkan tombol
3. Baru saat mentor menekan tombol, token dikonsumsi (`UsedAt` ditandai di backend)

> Ini contoh bagus untuk presentasi: keputusan desain yang tidak kelihatan di UI, tapi
> menentukan apakah fiturnya jalan di dunia nyata atau tidak.

**Sumber:** `frontend/src/app/api/auth/magic-link/route.ts`

---

## BAGIAN 3 — KETERIKATAN ROLE (4 lapis)

Role tidak "dicek sekali lalu dipercaya". Dia ditegakkan di empat tempat berbeda.

### Lapis 1 — Klaim di dalam JWT

Saat token diterbitkan, `VokasiaClaimsFactory` menempelkan:

```
sub        → ID user
role       → SuperAdmin | TenantAdmin | DeptHead | Teacher | IndustryMentor | Student
tenant_id  → ID sekolah  (null untuk SuperAdmin & mentor)
name       → nama tampilan
```

### Lapis 2 — Route guard di frontend

```ts
// frontend/src/lib/guard.ts
export const SEGMENT_ROLES = {
  "/sa":      ["SuperAdmin"],
  "/app":     ["TenantAdmin", "DeptHead", "Teacher"],
  "/mentor":  ["IndustryMentor"],
  "/student": ["Student"],
};
```

Publik tanpa login: `/` · `/login` · `/p/*` · `/verify/*`

Keputusannya tiga cabang:

- Halaman publik → **izinkan**
- Belum login → redirect `/login?next=<tujuan>` (tujuan asal disimpan, jadi setelah
  login user kembali ke tempat yang dia mau, bukan ke beranda)
- Login tapi role salah → redirect ke **rumahnya sendiri**, bukan halaman error

> Cabang ketiga itu keputusan UX yang layak disebut. Siswa yang tidak sengaja membuka
> `/app` tidak dihadapkan pesan "Akses ditolak" — dia cuma mendarat di `/student`. Tidak
> ada jalan buntu.

`resolveGuardDecision()` sengaja dibuat **fungsi murni** — tidak menyentuh Next.js,
tidak menyentuh DB — supaya bisa diuji unit langsung (`guard.test.ts`).

### Lapis 3 — Policy RBAC di API

```csharp
// backend/src/Vokasia.Api/Auth/RbacPolicies.cs
SaOnly              → role == SuperAdmin
TenantAdminOnly     → role == TenantAdmin        + tenant_id GUID valid
DeptHeadPlus        → TenantAdmin | DeptHead     + tenant_id GUID valid
TeacherPlus         → TenantAdmin | DeptHead | Teacher + tenant_id GUID valid
StudentSelf         → role == Student            + tenant_id GUID valid
TenantMember        → tenant_id GUID valid
MentorOwnPlacement  → role == IndustryMentor     + Placement.MentorUserId == sub
```

Dua hal yang patut disebut:

**(a) `tenant_id` harus GUID yang benar-benar valid, bukan sekadar ada.**
Komentar di kode menjelaskan alasannya: klaim yang rusak membuat konteks tenant jadi
`null`, dan tenant `null` **mematikan filter tenant** di ORM. Jadi memeriksa "ada atau
tidak" tidak cukup — harus bisa di-parse jadi GUID.

**(b) Mentor divalidasi per penempatan, bukan per sekolah.**
`MentorOwnPlacement` bukan pemeriksaan role biasa — dia *resource-based*: objek
`Placement`-nya sendiri diserahkan ke handler, lalu dibandingkan `MentorUserId == sub`.

Sebabnya struktural: **mentor industri sengaja lintas-tenant** (`TenantId = null`).
Satu bengkel bisa menerima siswa dari tiga SMK berbeda. Kalau mentor difilter per
sekolah, dia harus punya tiga akun. Jadi dia difilter per penempatan.

### Lapis 4 — Filter tenant otomatis di ORM

```csharp
// backend/src/Vokasia.Api/Auth/TenantResolutionMiddleware.cs
tenantContext.UserId   = claim "sub"
tenantContext.Role     = claim "role"
tenantContext.TenantId = claim "tenant_id"
```

Middleware ini mengisi konteks tenant **dari klaim JWT saja** di setiap request. Dari
situ, EF Core global query filter otomatis menyaring **setiap query** ke sekolah tersebut.

> Kalimat untuk presentasi: *"Saya tidak menulis `WHERE tenant_id = ...` di satu pun
> query. Kalau saya menulisnya manual, satu kali lupa berarti data sekolah A bocor ke
> sekolah B. Filternya dipasang di lapisan ORM — otomatis, tidak bisa lupa."*

Dua pengecualian yang **sengaja**, dan keduanya terdokumentasi di kode:

- **Mentor** — `TenantId = null`, jadi filter mati; digantikan pemeriksaan per placement
- **Endpoint publik** (`/api/verify/{certCode}`) — tidak ada JWT, jadi filter mati; itu
  memang tujuannya

### Ringkasan pemetaan role → halaman

```ts
// frontend/src/lib/roleHome.ts
SuperAdmin     → /sa
TenantAdmin    → /app
DeptHead       → /app
Teacher        → /app
IndustryMentor → /mentor
Student        → /student
(tak dikenal)  → /login   ← fallback aman
```

---

## BAGIAN 4 — SETIAP REQUEST SETELAH LOGIN

Frontend **tidak pernah** memanggil API secara langsung. Semua lewat satu pintu:
`/api/proxy/[...path]`.

| # | Yang terjadi |
|---|---|
| 1 | Baca cookie `vok_sess` → dapat ID sesi. Kosong → **401** |
| 2 | Ambil sesi dari Redis → dapat access token. Tidak ada → **401** |
| 3 | Tempelkan `Authorization: Bearer <token>`, teruskan ke API |
| 4 | Kalau API balas **401** → jalankan refresh **satu kali** → ulang request **satu kali** |
| 5 | Teruskan respons apa adanya ke browser |

Tiga detail yang layak disebut:

- **Body request dibaca sekali di awal** dan disimpan sebagai buffer. Sebabnya: body
  request adalah stream sekali-baca — kalau tidak di-buffer, percobaan kedua setelah
  refresh akan mengirim body kosong.
- **Refresh maksimal satu kali.** Tidak ada loop. Gagal ya gagal.
- **Token tidak pernah menyentuh browser.** Ini konsekuensi langsung dari langkah 1–3,
  bukan janji.

**Sumber:** `frontend/src/app/api/proxy/[...path]/route.ts`, `frontend/src/lib/refresh.ts`

---

## BAGIAN 5 — VERIFIKASI SERTIFIKAT

Alur paling pendek di seluruh sistem, dan paling penting untuk demo.

### Jalannya

```
Sertifikat PDF (ada QR)
        ↓  scan pakai HP mana pun
https://<domain>/verify/{certCode}
        ↓
GET /api/verify/{certCode}     ← anonim, tanpa JWT, rate-limited
        ↓
Query lintas SEMUA tenant:
  Certificate → Placement → Student → Company → Period → Tenant
        ↓
200 { nama siswa, nama sekolah, nama DUDI, nama periode, tanggal terbit, valid }
atau
404
```

### Empat keputusan desain yang harus kamu bisa jelaskan

**(1) Anonim, sengaja.**
Yang memverifikasi adalah HRD perusahaan yang sedang memegang lamaran — orang yang tidak
punya dan tidak boleh punya akun di sistem sekolah. Kalau verifikasi butuh login,
fiturnya mati.

**(2) Filter tenant mati, sengaja.**
Tidak ada JWT → konteks tenant `null` → filter EF mati → pencarian menjangkau semua
sekolah. Itu memang tujuannya: siapa pun harus bisa memverifikasi sertifikat sekolah
mana pun.

> Ini kelihatan seperti lubang keamanan sampai kamu jelaskan. Siapkan kalimatnya:
> *"Filternya memang mati di sini, dan itu disengaja — kalau tidak, HRD di Surabaya
> tidak bisa memverifikasi sertifikat dari sekolah di Jakarta."*

**(3) Kode salah → 404, bukan `200 {valid: false}`.**
Supaya tidak ada beda sinyal antara "salah ketik" dan "kode ada tapi tidak valid".
Di skema saat ini tidak ada status *revoked* — kode yang tersimpan selalu valid sejak
diterbitkan. Jadi "ada di DB" = valid, "tidak ada" = 404 murni.

Pola yang sama dipakai di tempat lain: `MarkRead` untuk notifikasi milik orang lain juga
balas **404, bukan 403** — supaya tidak membocorkan bahwa ID itu ada.

**(4) Rate limit aktif.**
`CertCode` acak bisa ditebak dengan brute force. Endpoint ini memakai policy rate limit
`public` yang sama dengan validasi magic link.

### Yang TIDAK dikembalikan

Sesuai FR-CRT-02 dan UU PDP: **tanpa NISN, tanpa kontak, tanpa nilai.** Cukup untuk
menjawab "apakah orang ini benar PKL di sana", tidak lebih.

**Sumber:** `backend/src/Vokasia.Api/Endpoints/CertificateEndpoints.cs`

---

## BAGIAN 6 — CERITA BUG YANG LAYAK DISEBUT

Kalau ada waktu, ini bahan cerita bagus di babak teknologi. Terdokumentasi di
`DECISIONS.md` D28.

> Alur login jalan sempurna waktu saya jalankan frontend dan API langsung di laptop.
> Begitu frontend saya masukkan ke container, login patah total.
>
> Sebabnya: URL `/connect/authorize` itu dikirim sebagai **redirect ke browser**, bukan
> dipanggil server-ke-server. Saya memakai `API_INTERNAL_URL` — di Docker Compose nilainya
> `http://api:8080`, nama DNS internal Docker. Browser di laptop jelas tidak bisa
> me-resolve nama itu.
>
> Kenapa tidak ketahuan lebih awal? Karena di mode pengembangan lokal, `API_INTERNAL_URL`
> dan `API_PUBLIC_URL` kebetulan bernilai sama — `localhost:5000`. Bug-nya baru muncul
> ketika kedua nilai itu berbeda.

Pelajarannya, dan ini yang sebenarnya kamu jual: **jalan di laptop bukan bukti jalan di
produksi.** Bug ini cuma bisa ditemukan lewat pengujian terhadap stack Docker sungguhan.

---

## BAGIAN 7 — LATIHAN SEBELUM PRESENTASI

Kalau ada yang bertanya "kamu paham kodenya?", tiga alur ini yang harus kamu bisa
gambar di papan tulis tanpa membuka laptop:

1. **Login PKCE 9 langkah** — terutama: kenapa `state` disimpan di Redis dan dikonsumsi
   sekali (anti CSRF & replay), dan kenapa ada dua cookie
2. **Empat lapis role** — klaim → cookie lite → guard frontend → policy API + filter ORM
3. **Verifikasi sertifikat** — kenapa anonim, kenapa filter tenant mati, kenapa 404
   bukan `valid:false`

Kalau kamu lancar di tiga ini, pertanyaan "AI yang nulis kodenya kan?" akan langsung
kehilangan tenaganya.
