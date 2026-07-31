# Audit Vokasia v5 — Verifikasi v4 + Gap Baru di Luar v1–v4

Pass ini punya dua tugas: (1) memastikan keenam fix v4 benar-benar bekerja; (2) berburu masalah **baru yang belum pernah disentuh v1–v4**. Jujur di depan: v5 ini **pendek** — bukan karena kurang teliti, tapi karena setelah v1–v4 codebase sudah kuat. Aku hanya menemukan **satu** gap baru yang substansial (dan kode kalian sendiri sudah menandainya lewat TODO). Aku tidak memadatkan dokumen ini dengan temuan palsu.

**Tanggal**: 30 Juli 2026 · **Metode**: kode + DB + probe HTTP. (Batas sama: aku tak bisa login sendiri; area terautentikasi dinilai dari kode, bukan render visual.)

---

## 1. Verifikasi fix v4 — SEMUA KOKOH

| Fix v4 | Verifikasi | Status |
|---|---|---|
| **Object storage tenant-scoped** | `ObjectStorageKeyPolicy.IsOwnedKey` cek prefix `tenant/{tid}/{ns}/` **plus** tolak `\` & leading `/` (bonus: pertahanan path-traversal). Dipanggil di Journal/Visit/Billing endpoints **+ Worker `PhotoUploadedConsumer` + `PortfolioEndpoints`**. Persis rantai yang kuflag di v4 §2.1, ditutup lengkap. | ✅ |
| **Teacher scope** | `AddTeacherComment`/`ListJournalsForTeacher` kini `TeacherPlacementScope.CanAccess(role, userId, placement.TeacherId)` — role-aware (Teacher → hanya placement-nya; TenantAdmin/DeptHead → lintas). Persis titik-bahaya "jangan kunci admin" yang kuflag, ditangani benar. | ✅ |
| **Kuota Serializable** | `BeginSerializableQuotaTransactionAsync` + `TryReserveSlot` + `CommitAsync` + `catch when IsQuotaConcurrencyConflict → 409`. Bulk ikut hitung reservasi pending. TOCTOU ditutup. | ✅ |
| **Bobot rubrik** | `RubricValidation.HasValidWeights` = `Count>0 && All(0..100) && Sum==100`. Menolak negatif & >100. | ✅ |
| **Finalisasi assessment** | `Assessment.IsFinal` diberi `[ConcurrencyCheck]` → dua finalize bersamaan → salah satu `DbUpdateConcurrencyException`. | ✅ |
| **Overflow heading/tombol mobile** | `Button` kini `whitespace-nowrap`; heading di `globals.css` diperketat. | ✅ |

Tidak ada fix v4 yang salah terap. Beberapa malah melampaui rekomendasi (pertahanan traversal pada key policy).

---

## 2. TEMUAN BARU (di luar v1–v4)

### V5-1 — RENDAH–MENENGAH — Nonaktifkan akun/tenant tidak mencabut sesi aktif

**Ini satu-satunya gap substansial yang baru. Kode kalian sudah menyadarinya (TODO).**

`IsActive` hanya dicek di **titik tukar-kredensial-jadi-token**: login form (`AccountEndpoints.cs:547`), issuance token (`AuthorizationController.cs:109`), impersonasi (`:205`), magic-link (`MagicLinkService.cs:159`). Saat `DeactivateUser` (`SchoolUsers.cs:110`) atau `DeactivateTenant` (`SaTenantsEndpoints.cs:249-255`) menyetel `IsActive=false`, **tidak ada sesi/token yang dicabut**. `DeactivateTenant` bahkan memuat komentar eksplisit: `// TODO-H2E3: cabut session Redis`.

**Kabar baiknya — dampaknya BOUNDED, bukan bencana**: grant `refresh_token` **mengecek ulang `IsActive`** (`AuthorizationController.cs:105-118` → `InvalidGrant` jika nonaktif). Karena access token hanya **15 menit** (NFR-SEC-01), begitu access token milik akun nonaktif kedaluwarsa, BFF gagal refresh → akses hilang. Jadi jendela akses residual ≈ **sisa umur access token (≤15 menit)**, bukan 14 hari.

**Kapan 15 menit itu penting**: offboarding berisiko tinggi — akun terkompromi, atau insiden safeguarding yang melibatkan minor — di mana admin menonaktifkan akun dan mengharap akses **seketika** putus, bukan 15 menit. Untuk konteks sekolah + data anak, pencabutan seketika layak.

**Perbaikan (kecil — building block-nya SUDAH ADA)**: pada deactivate, hapus sesi Redis milik user. Indeks `user-sessions:{userId}` (dibangun `createSession` di `bffSession.ts`) memetakan userId → daftar sessionId **persis untuk keperluan ini** — tinggal iterasi & `del`. Untuk `DeactivateTenant`, lakukan untuk semua user tenant. Opsional: revoke refresh token OpenIddict mereka (reference token, revocable) agar refresh langsung mati.

> **⚠ Titik bahaya saat memperbaiki**: (1) `DeactivateTenant` menonaktifkan **semua** user tenant — pencabutan sesi massal harus dibatch/aman dari timeout kalau tenant besar (ratusan user). (2) Jangan cabut sesi **SuperAdmin** yang sedang menjalankan aksi (dia bukan bagian tenant, tapi pastikan filter userId benar). (3) Kalau ikut me-revoke refresh token OpenIddict, lakukan best-effort (try/catch) — kegagalan revoke jangan menggagalkan transaksi deactivate; `IsActive=false` + hapus sesi Redis sudah cukup sebagai sumber kebenaran. (4) Reaktivasi (`IsActive=true` lagi) tidak memulihkan sesi lama (sudah terhapus) — user harus login ulang; itu perilaku yang benar, dokumentasikan saja.

---

## 3. Area baru yang DIPERIKSA dan ternyata AMAN

Supaya jelas cakupannya (dan tidak dikira belum dicek):

- **EXIF/GPS stripping** — `PhotoProcessor.Process` men-null-kan `ExifProfile/IptcProfile/XmpProfile` lalu re-encode JPEG; `Tenant.GeotagAllowed` **default `false`** → GPS minor di-strip secara default, hanya dipertahankan bila tenant opt-in eksplisit. Sesuai NFR-SEC-05/PDP. ✓
- **Notification `MarkRead`** — cek `notif.UserId != caller → 404` (pola privasi, tak bocorkan keberadaan). Tak bisa tandai notifikasi user lain. ✓
- **CORS** — sengaja **tidak** dikonfigurasi; API hanya dipanggil server-side lewat BFF proxy, tak ada origin lintas-situs dari browser → tak ada permukaan CORS permisif. Benar. ✓
- **Upload non-gambar (risiko SVG/HTML → stored XSS di portofolio publik)** — content-type whitelist (`image/jpeg|png|webp`, tanpa SVG) di `UploadRequestValidator`; **plus** worker RE-DECODE + RE-ENCODE tiap gambar (ImageSharp) dan portofolio hanya menampilkan `Status == Processed`. File non-gambar gagal decode → `Failed` → tak pernah tampil. Pertahanan berlapis. ✓
- **Batas auth & format error** — 8 endpoint terproteksi → `401` `application/problem+json`. ✓

---

## 4. Tindak lanjut implementasi

- **V5-1 ditutup**: `DeactivateUser` dan `DeactivateTenant` sekarang menghapus sesi BFF Redis melalui kontrak `sess:{sessionId}` + `user-sessions:{userId}`. Revocation tenant diproses batch 64 user, best-effort, dan hanya user dengan `TenantId` tenant yang dinonaktifkan (SuperAdmin tidak ikut tersentuh).
- `DeactivateUser` juga menolak target lintas tenant sehingga TenantAdmin tidak dapat menonaktifkan akun global/tenant lain.
- Compose dev dibuild ulang dan seluruh container healthy; `/health` 200, root frontend 200, dan API 404 tetap `application/problem+json`. Runtime GSSAPI warning dibersihkan dengan paket `libgssapi-krb5-2` pada image API/worker.
- Residual tetap: gunakan `docker-compose.prod.yml` dengan secret DataProtection/certificate, URL publik, dan forwarded-host allowlist; warning DataProtection/HTTPS pada Compose dev memang expected. Full .NET integration/async suite masih terblokir environment Testcontainers (`endpoint is not a npipe URI`), bukan assertion regresi fitur ini.

## 5. Kesimpulan kesiapan produksi

Setelah v1–v5, permukaan aplikasi Vokasia **matang dan konsisten**. Temuan tiap putaran mengecil dan makin sempit — tanda proses hardening yang sehat. Tidak ada lagi lubang keamanan lebar yang kutemukan; V5-1 pun bounded (≤15 menit) dan building block perbaikannya sudah ada.

**Sebelum go-live, yang tersisa adalah verifikasi & konfigurasi, bukan bug arsitektural:**
1. V5-1: cabut sesi Redis saat deactivate (kecil, disarankan untuk konteks safeguarding).
2. Residual yang kalian sudah lacak: E2E/Lighthouse terautentikasi di 320/375/414/768; env produksi wajib (cert passwords, public URL, forwarded-host allowlist); pakai `docker-compose.prod.yml` (compose dasar masih ekspos port dependency — itu memang untuk dev); advisory NuGet MailKit/MimeKit/Microsoft.OpenApi.
3. Verifikasi yang **aku** tak bisa lakukan (jangan dianggap lulus): render visual area terautentikasi, uji konkurensi live (kuota/finalize di bawah beban paralel), `dotnet test`/`bun test` penuh (Testcontainers `npipe` masih blocker), perilaku runtime prod compose sebenarnya.

Rekomendasi: tutup V5-1, jalankan checklist #2–#3, lalu Vokasia siap rilis terbatas (pilot satu-dua sekolah) sambil memantau. Itu urutan yang wajar untuk platform yang memegang data siswa.
