# VOK-H3-E3 — Immutability + validasi menyeluruh + rate limit

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-3 `backend/` | `h3-eng3-immutability-validation` | GPT-5.4 Thinking | **max** | M2 | PRD FR-JRN-04, NFR-SEC-06/08 |

## Tugas

Tiga lapis proteksi domain: jurnal Approved tidak bisa diubah siapa pun, seluruh input tervalidasi FluentValidation, rate limit login & endpoint publik. Plus test yang membuktikan ketiganya.

## Implementasi

### 1. Immutability — `Vokasia.Domain/`
- `JournalEntry.EnsureMutable()` — tujuan: method domain: `Status==Approved` → throw `DomainImmutableException("journal-approved-immutable")`; dipanggil SEMUA path mutasi (update teks, attach foto, re-submit, delete).
- `DomainImmutableException → 409` (exception middleware mapping) — tujuan: respons konsisten `{code, message}` — bukan 500.
- `AssessmentImmutabilityGuard` (kerangka) — tujuan: pola sama untuk nilai final (dipakai penuh H5); `IsFinal==true` → tolak mutasi skor.
- **Tanpa endpoint unlock** — unlock ber-audit = fase 2 (NFR-SEC-08); tulis komentar penanda di kode.

### 2. Validasi — `Vokasia.Api/Validation/` (FluentValidation, auto-register)
- `SubmitJournalValidator` — Text NotEmpty ≤500; CompetencyIds 1–5 & milik major siswa; PhotoIds ≤3.
- `CreatePeriodValidator` — tanggal valid, Start<End, ClassLevels ⊆ {X,XI,XII}.
- `CreatePlacementValidator` · `ImportStudentRowValidator` (dipakai per baris CSV) · `InviteUserValidator` (email format+role whitelist) · `ProposeCompanyValidator` · `UploadRequestValidator` (ContentType whitelist, size ≤5MB) · `RejectJournalValidator` (reason 5–300 kar).
- `ValidationFilter` (endpoint filter global) — tujuan: request tanpa validator terdaftar di scope H1–H3 → fail CI test `AllRequestsHaveValidatorsTest` (mencegah endpoint lolos telanjang).
- Sanitasi: `TextSanitizer.Clean(string)` — tujuan: strip HTML/script dari semua field teks bebas (jurnal, komentar, alasan) sebelum simpan.

### 3. Rate limit — `Vokasia.Api/RateLimiting/`
- `AddVokasiaRateLimiting(IServiceCollection s)` — tujuan: policy `"login"` 5/mnt per IP+username (sliding), `"public"` 10/mnt per IP (`/verify`, `/p`, magic link exchange); 429 + `Retry-After`.
- Terpasang di: `/connect/token` (password/code grant), `/api/auth/magic/*`, endpoint publik.

### 4. Test — `Vokasia.Tests/Guard/`
- `ImmutabilityTests` — update/attach/delete pasca-Approved oleh siswa, mentor, TenantAdmin → semua 409; Rejected → boleh isi ulang.
- `ValidatorCoverageTests` — refleksi: semua request type H1–H3 punya validator.
- `ValidationBoundaryTests` — 500 vs 501 kar; foto ke-3 vs ke-4; content-type `application/x-msdownload` → tolak.
- `RateLimitTests` — login ke-6 dalam 1 mnt → 429; setelah jendela → pulih.
- `SanitizerTests` — `<script>`, event handler inline → bersih.

## Acceptance Criteria

- Semua test §4 hijau; immutability terbukti untuk 3 role.
- Given payload `<script>alert(1)</script>` di jurnal, Then tersimpan bersih (tanpa tag).
- Given 429, Then body & header `Retry-After` konsisten.

## DoD + verifikasi runner (max)

Suite penuh 2× (kedua dari state bersih) → negative test manual curl (mutasi jurnal approved via 3 role, login brute 6×) → audit AGENTS §4/§5 → PROMPT D → setor.
