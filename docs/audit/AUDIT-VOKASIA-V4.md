# Audit Vokasia v4 — Pengujian Total Kesiapan Produksi

Pass menyeluruh **setelah seluruh fix v1–v3 diterapkan**. Tujuan: (a) memverifikasi fix benar-benar bekerja, bukan sekadar diklaim; (b) berburu bug baru di area yang belum pernah dibedah (otorisasi tingkat-objek, konkurensi, validasi, lapisan worker). Tiap fix diberi catatan **⚠ Titik bahaya** — risiko regresi saat memperbaiki.

**Tanggal**: 30 Juli 2026 · **Target**: instance berjalan (`api:5000`, `frontend:3000`, mode Dev) + kode + DB.

## 0. Batas metodologi (harus dibaca)

Satu hal tetap di luar jangkauanku **walau Developer memberi wewenang penuh**: aku **tidak bisa melakukan login** — lapisan keamananku memblokir pengetikan password, submit password via curl, maupun konsumsi token magic-link. Menghapus cookie tidak membukanya (yang diblokir aksi login, bukan cookie). Konsekuensinya: **area di balik autentikasi tidak bisa kulihat ter-render langsung** oleh sesi-ku (dan sejak `vok_session` kini ditandatangani, memalsukan sesi pun benar ditolak). Karena itu temuan di bawah bersumber dari **kode + query DB + probe HTTP + uji serangan** — yang untuk berburu bug keamanan justru lebih dalam daripada klik UI. Verifikasi visual area terautentikasi tetap memerlukan Developer login lalu aku menyetir sesinya.

---

## 1. Fix v1–v3 yang TERVERIFIKASI KOKOH (bukti sesi ini)

Disebut lebih dulu agar proporsional. Semua diverifikasi langsung.

| Area | Bukti |
|---|---|
| **`vok_session` ditandatangani** | Probe live: cookie lite palsu `{role:"SuperAdmin"}` → `/sa` **redirect ke `/login?error=access_required`** (dulu render shell SA). Forgery ditutup. |
| **RFC7807 seragam** | `401` (`/api/notifications`, `/sa/tenants`) & `404` (`/api/verify/XX`) semuanya `application/problem+json`. Menutup temuan v3 §8 (dulu body kosong). |
| **ForwardedHeaders aman** | `ForwardedHeadersSetup.cs`: `ForwardLimit=1`, `KnownProxies`/`KnownIPNetworks` wajib eksplisit, default loopback. `X-Forwarded-For` tak bisa dipalsu klien luar → rate-limiter IP tak bisa dilewati. (Ini justru tempat paling sering salah — diterapkan benar.) |
| **Guard produksi utuh** | `MapOpenApi()` gated `IsDevelopment()`; `UseHsts()` gated `!Dev && !Testing`; `seed demo` **melempar exception** kalau bukan Dev/Testing (`Program.cs:185-187`, fail-closed). |
| **Shell mentor/siswa responsif** | `max-w-[1600px]` + `<aside class="sticky top-0 h-screen ... lg:flex">` + `WorkspaceSidebar` + `RoleMobileNav ... hideAtDesktop`. Rekomendasi v3 §11-quinquies diterapkan benar. |
| **Panel notifikasi sadar-viewport** | `NotificationPanel` kini `align === "left" ? "left-0" : "right-0"`. Menutup v3 §11c.3. |
| **Sidebar sticky** | `app/(school)/app/layout.tsx` `<aside class="sticky top-0 h-screen">`. Menutup v3 §11c.2. |
| **Impersonation teraudit benar** | `VokasiaDbContext.SaveChangesAsync`: saat `ImpersonatorUserId` ada, tiap AuditLog baru → `ActingAsUserId=target`, `ActorUserId=SA asli`. |
| **Refresh token** | `UseReferenceRefreshTokens()` (opaque, revocable) + `SetRefreshTokenReuseLeeway(Zero)` → rotasi + deteksi reuse. |
| **Bounds skor 0–100** | `SubmitScoresAsync`: `s.Value < 0 || s.Value > 100 → BadRequest`. |
| **Presign server-generated** | Object key upload = `tenant/{tid}/journal/{guid}` (server), klien hanya kirim ContentType (whitelist). Tak ada traversal/overwrite. |
| **Race dobel-submit jurnal** | `JournalEntry.SlotId` **unique index** → submit konkuren → DbUpdateException, bukan dobel baris. |
| **ApproveJournal/RejectJournal** | Auth polos dikompensasi `authService.AuthorizeAsync(user, placement, MentorOwnPlacement)` di handler — mentor hanya placement-nya sendiri. |
| **Batas auth** | 8 endpoint terproteksi diuji tanpa token → semua `401`. |

Ini banyak, dan solid. Perimeter keamanan Vokasia matang.

---

## 2. TEMUAN TERBUKA (urut severity, tiap fix + titik bahaya)

### 2.1 TINGGI — IDOR referensi object storage (dibawa dari v3 §13.2, MASIH TERBUKA)

**Terkonfirmasi masih terbuka**: grep `JournalEndpoints.cs` + `Validation/` untuk validasi prefix `tenant/` pada object key → hanya baris presign (server) yang muncul; **tidak ada** validasi pada key dari klien.

Tiga endpoint menyimpan object key dari klien **tanpa memvalidasi milik namespace tenant pemanggil**:
- `AttachPhoto` — `AttachPhotoRequest(string ObjectKey)` (`Dtos.cs:60`)
- `CreateVisit` — `req.PhotoKey` (`VisitEndpoints.cs:86`)
- `UploadPaymentProof` — `req.ObjectKey → invoice.ProofKey` (`BillingEndpoints.cs:104`)

Rantai terparah (jurnal → publik): siswa attach foto ke jurnalnya sendiri dengan `ObjectKey` = objek sembarang di bucket → worker `PhotoUploadedConsumer.cs:69` `GetObjectAsync` mentah → saat portofolio dipublish, `PortfolioEndpoints.cs:247` `PresignedGetObjectAsync` atas key itu → **objek lintas-tenant tampil di `/p/{slug}` publik**. Invarian "key milik tenant pemilik" diasumsikan, tak pernah ditegakkan.

Severity jujur: eksploitasi tertarget butuh tahu key korban (GUID ganda, sulit ditebak); tapi ini broken object-level authorization nyata (OWASP A01) bermuara ke halaman publik memuat data minor.

**Perbaikan**: validator bersama — object key dari klien wajib berawalan `tenant/{callerTenantId}/{prefix}/`. Idealnya worker & portofolio juga menolak key di luar prefix (pertahanan berlapis).

> **⚠ Titik bahaya saat memperbaiki**: (1) **Object key lama** yang sudah tersimpan (mis. seed/data uji, atau yang kubuat manual untuk QA) mungkin **tidak** mengikuti format prefix baru — validasi ketat bisa membuat portofolio lama gagal render. Terapkan validasi hanya di jalur **tulis baru**, jangan retroaktif menolak baca key lama tanpa migrasi. (2) Jalur **visit signature** memakai key server-generated (`visit-signature/`) berbeda prefix dari `visit-photo/` — pastikan whitelist mencakup semua prefix sah per endpoint, jangan hanya `journal/`. (3) Format key memakai `:N` GUID tanpa tanda hubung — cocokkan regex persis, jangan asумsикан format GUID standar.

### 2.2 MENENGAH — Endpoint guru tidak ter-scope ke placement bimbingannya (BARU)

`ListJournalsForTeacher(Guid placementId)` (policy `TeacherPlus`) hanya cek `placementExists` di tenant — **tidak** cek `placement.TeacherId == caller.UserId`. Sama untuk `AddTeacherComment`: hanya cek `tenant.UserId.HasValue`, tak cek kepemilikan placement.

Akibat: **guru mana pun bisa membaca seluruh riwayat jurnal + komentar, dan menulis komentar, pada siswa mana pun sesekolah** — bukan hanya bimbingannya. UI membatasi (halaman "Bimbingan Saya" ter-scope benar), tapi **API tidak** — otorisasi ditegakkan di UI saja. Seorang guru yang memanggil API langsung dengan `placementId` lain menembusnya. Ini memperkuat pola v3 §11c.1: scoping guru tegak di sebagian tempat, bocor di tempat lain.

Severity: intra-tenant (sesekolah), peran guru (semi-tepercaya), tapi melanggar least-privilege yang produk ini sendiri maksudkan.

**Perbaikan**: di kedua handler, kalau peran = `Teacher` (bukan TenantAdmin/DeptHead), tegakkan `placement.TeacherId == caller.UserId` → else `Forbid`. TenantAdmin/DeptHead tetap boleh lintas (memang tugasnya).

> **⚠ Titik bahaya**: (1) `TeacherPlus` mencakup **TenantAdmin & DeptHead** yang MEMANG harus melihat semua — jangan tegakkan `TeacherId==caller` untuk mereka, nanti admin malah kehilangan akses. Cabang berdasarkan `tenant.Role`. (2) Placement bisa **berpindah guru** (`AssignTeacher`) — komentar/histori lama dari guru sebelumnya tetap harus terbaca; scope pada `placement.TeacherId` **saat ini** sudah benar, tapi pastikan tidak menghapus komentar guru lama. (3) Halaman "Bimbingan" memakai daftar ter-scope; setelah API diperketat, pastikan UI tidak pernah menaut ke placement non-bimbingan (kalau ada, akan mulai 403 — ubah UI dulu).

### 2.3 RENDAH–MENENGAH — Kuota placement bisa dilewati saat konkuren (TOCTOU, BARU)

`CheckQuotaOnPlacementAsync`: `CountAsync(active) >= maxPlacements` lalu `CreatePlacement` → `db.Placements.Add()` **tanpa transaksi/isolasi**. Dua `CreatePlacement` bersamaan di ambang kuota sama-sama lolos cek → dua-duanya insert → **kuota plan terlampaui**. Integritas bisnis/billing (tenant melebihi batas berbayar), bukan keamanan.

**Perbaikan**: bungkus cek+insert dalam transaksi `Serializable`, atau tegakkan batas via constraint/`INSERT ... WHERE (SELECT count ...) < max` di DB.

> **⚠ Titik bahaya**: (1) `BulkCreatePlacements` memanggil jalur yang sama dalam loop — transaksi Serializable + retry perlu diterapkan agar bulk tidak deadlock atau gagal separuh. (2) Serializable menaikkan risiko **serialization failure** di Postgres pada beban tinggi — butuh retry policy (pola yang **sudah ada** di `SaCompaniesEndpoints.MergeCompanies` — tiru dari sana, jangan bikin baru). (3) Tenant tanpa `PlanId` = tanpa batas (by design) — jangan tak sengaja memaksa batas 0 pada mereka.

### 2.4 RENDAH — Bobot rubrik per-aspek boleh negatif (BARU)

`WeightsSumTo100` (`RubricEndpoints.cs:36`) hanya cek `Sum == 100`, **tidak** cek tiap `Weight ≥ 0`. Rubrik `[200, -100]` lolos → `ComputeWeightedScore = Σ(value×weight)/100` dengan bobot negatif → nilai akhir bisa termanipulasi, di luar 0–100, atau negatif. Pembuat rubrik = `TenantAdminOnly` (tepercaya) → severity rendah (self-inflicted), tapi tetap gap integritas.

**Perbaikan**: `WeightsSumTo100` juga wajibkan `aspects.All(a => a.Weight >= 0)` (dan mungkin `<= 100`).

> **⚠ Titik bahaya**: minim — perubahan validasi murni. Pastikan hanya berlaku di jalur create/update rubrik; rubrik lama yang sudah tersimpan dengan bobot aneh (kalau ada) tidak otomatis divalidasi ulang.

### 2.5 RENDAH — Finalisasi penilaian: check-then-act tanpa concurrency token (dari v3 §13.3)

`FinalizeAssessment`: `if (assessment is { IsFinal: true }) continue;` idempoten, tapi baca-lalu-tulis tanpa rowversion → dua finalize bersamaan bisa lolos. Dampak nyata teredam idempotency consumer sertifikat. Tambah `[ConcurrencyCheck]`/`xmin` pada `Assessment.IsFinal`.

> **⚠ Titik bahaya**: menambah rowversion mengubah skema (migrasi EF) — pastikan migrasi additive, tak memutus baris Assessment yang ada.

---

## 3. Prioritas produksi (pass v4)

| # | Temuan | Tingkat | Effort | Status |
|---|---|---|---|---|
| 2.1 | Validasi prefix-tenant object key (3 endpoint + worker/portfolio) | Tinggi | Kecil | Terbuka (dari v3) |
| 2.2 | Scope guru ke placement bimbingan (`ListJournalsForTeacher`, `AddTeacherComment`) | Menengah | Kecil | Baru |
| 2.3 | Transaksi/serialisasi pada kuota placement | Rendah–Menengah | Kecil | Baru |
| 2.4 | Bobot rubrik per-aspek ≥ 0 | Rendah | Sangat kecil | Baru |
| 2.5 | Concurrency token `Assessment.IsFinal` | Rendah | Kecil | dari v3 |

**Rekomendasi urutan**: 2.1 dulu (satu-satunya bermuara ke halaman publik + data minor), lalu 2.2 (scoping least-privilege yang produk maksudkan). 2.3–2.5 hardening yang bisa menyusul.

---

## 4. Residual yang Developer sudah tandai (tidak diulang, tetap berlaku)

- Tenant filter fail-open saat `TenantId` null pada proses system/worker — perlu explicit system-scope + endpoint whitelist.
- Suite integration/async penuh belum jalan (Testcontainers `npipe` Docker Windows) — infrastruktur, bukan assertion.
- E2E area login, seed komersial lengkap, portfolio/cert valid, logout multi-session, Lighthouse/PWA device — belum terverifikasi penuh.
- NuGet advisories: `Microsoft.OpenApi` (high), MailKit/MimeKit (moderate) — keputusan upgrade dependency.
- Belum ada commit; worktree kotor dipertahankan.

---

## 5. Verifikasi yang tidak bisa kulakukan sesi ini (jangan dianggap lulus)

- Render visual area terautentikasi (`/app` `/sa` `/mentor` `/student`) — login classifier-blocked, forgery kini benar ditolak.
- Uji konkurensi 2.3/2.5 secara live (butuh sesi terautentikasi + beban paralel).
- `dotnet test`/`bun test` (tak ada toolchain di sandbox); angka test dari laporan Developer tak kuverifikasi ulang.
- Perilaku produksi sebenarnya (prod compose) — hanya guard kode yang kuverifikasi, bukan runtime prod.
