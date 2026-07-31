# VOK-H6-E3 — Impersonation ber-audit + hardening secrets + scan

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-3 lintas (koordinasi via task) | `h6-eng3-impersonation-hardening` | GPT-5.4 Thinking | **max** | M5 | PRD FR-AUTH-07, NFR-SEC-06/07 |

## Tugas

Impersonation SuperAdmin yang setiap jejaknya ter-audit, plus hardening pra-rilis: sweep secrets, dependency & container scan, security headers.

## Implementasi

### 1. Impersonation — `backend/Auth/` + BFF
- `StartImpersonation(Guid targetUserId) → ImpersonationSession` — policy `SaOnly`. Tujuan: session baru ber-claim `act_as={targetId}` + `actor={saId}`; TTL 1 jam; audit `ImpersonationStarted`; dilarang impersonate sesama SuperAdmin.
- `EndImpersonation()` — tujuan: kembali ke session SA + audit `ImpersonationEnded`.
- `AuditActorEnricher` (middleware) — tujuan: SEMUA `WriteAuditLog` saat impersonasi otomatis berisi `ActorUserId=SA, ActingAsUserId=target` — tak bergantung disiplin pemanggil.
- BFF: `POST /api/auth/impersonate {targetUserId}` + banner state — tujuan: cookie session ditukar; FE menerima flag `impersonating:{name}`.
- `ImpersonationBanner()` (frontend, komponen global) — tujuan: strip kuning "Anda sedang bertindak sebagai {nama} — [Akhiri]" di semua layar selama impersonasi.

### 2. Hardening
- Secrets sweep — tujuan: `git grep` pola (password=, key=, secret=, conn string) + cek `.env` tidak ter-commit + semua config via env (`IOptions`); temuan → perbaiki di ticket ini.
- `AddSecurityHeaders(app)` — tujuan: CSP dasar (self + MinIO host), `HSTS`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` minimal; verifikasi cookie session `HttpOnly Secure SameSite=Lax`.
- Verifikasi rate limit publik aktif di `/p/*`, `/verify/*`, magic link (pasangan H3-E3).
- Scan + laporan `backend/docs/security-scan-H6.md`:
  - `dotnet list package --vulnerable --include-transitive`
  - `bun audit` (atau `osv-scanner` lockfile)
  - `trivy image` untuk image api & worker & frontend
  - Tujuan: temuan High/Critical = 0, atau justifikasi tertulis per item → keputusan Developer.

### 3. Test — `Vokasia.Tests/Security/`
- `ImpersonationTests` — start → aksi → audit berisi actor+actingAs; TTL lewat → session mati; non-SA → 403; SA→SA → 403.
- `SecurityHeadersTests` — response publik & app memuat header wajib.
- `CookieFlagsTests` — session cookie httpOnly+Secure+SameSite=Lax.

## Acceptance Criteria

- Given SA impersonate TenantAdmin lalu ubah periode, Then AuditLog: actor=SA, actingAs=admin, action jelas; banner tampil di FE.
- Given scan, Then laporan tersedia; H/C = 0 atau ada justifikasi per item.
- Semua test §3 hijau.

## DoD + verifikasi runner (max)

Suite security penuh (termasuk milik H2/H3 — regresi) → jalankan 3 scan & lampirkan laporan → negative manual (impersonate sebagai TenantAdmin → 403) → PROMPT D → setor.
