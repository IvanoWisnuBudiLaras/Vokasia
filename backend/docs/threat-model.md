# Threat Model — Vokasia (v0.1, gate M0)

Acuan review security sepanjang sprint (SOUL.md, AGENTS.md §non-negotiable). Setiap butir dipetakan ke NFR-SEC-01..08 (PRD §Bagian NFR) sebagai checklist VPM.

## Aset

| Aset | Kenapa sensitif |
|---|---|
| Data anak (Student: NISN, nama, kelas) | Subjek UU PDP — anak di bawah umur (NFR-SEC-05) |
| Token (access/refresh JWT, magic link) | Kunci akses lintas 7 role; kompromi = takeover akun |
| Nilai & sertifikat (Assessment, Certificate) | Integritas akademik — dasar kelulusan PKL siswa |
| Foto jurnal/kunjungan (JournalPhoto, Visit) | Bisa memuat EXIF-GPS lokasi anak jika tidak di-strip |
| Data tenant lain (isolasi) | Kebocoran lintas sekolah = pelanggaran kontrak multi-tenant |

## Aktor jahat (threat actor)

| Aktor | Motif/kemampuan |
|---|---|
| Siswa iseng | Coba akses data siswa lain / ubah jurnal approved / bypass validasi |
| User tenant lain (TenantAdmin/Teacher sekolah B) | Sengaja/tidak sengaja query data tenant A via ID tebakan |
| Anonim (tanpa akun) | Scraping `/p/{slug}` `/verify/{code}`, brute force login, akses endpoint tanpa auth |
| Mentor palsu / magic link dicuri | Pakai link undangan bocor untuk approve jurnal / isi nilai palsu |
| SuperAdmin nakal (insider) | Impersonasi tanpa jejak audit (mitigasi H6-E3) |

## Permukaan serangan & mitigasi

| Permukaan | Risiko | Mitigasi | NFR-SEC |
|---|---|---|---|
| `/connect/authorize`, `/connect/token` | Code interception, replay | PKCE wajib (`RequireProofKeyForCodeExchange`), access 15 mnt, refresh rotation+reuse detection (H2-E3) | 01 |
| Browser storage | XSS mencuri token | Token TIDAK PERNAH di localStorage/sessionStorage — hanya httpOnly Secure SameSite=Lax cookie di BFF | 02 |
| Semua endpoint API tenant-scoped | Privilege escalation, akses lintas role | RBAC policy per endpoint (matrix 2.3), ditegakkan server-side bukan UI | 03 |
| Query data tenant-scoped | Kebocoran lintas tenant | EF global query filter `tenant_id` (stub H1, aktif penuh H2-E3) + `PlacementScopeHandler` mentor per-placement + test isolasi | 04 |
| Data Student, Portfolio publik | Bocor data anak / UU PDP | Field minimal on `Student`; portofolio opt-in tanpa NISN/kontak; EXIF-GPS strip default (H4); retensi foto 2 th (backlog ops) | 05 |
| Form input (jurnal, import CSV, komentar) | Injection, payload berbahaya | FluentValidation semua request; presigned upload (bukan body API); rate limit login 5/mnt, publik 10/mnt | 06 |
| Magic link mentor | Link bocor/diteruskan | Token sekali pakai, TTL 72 jam, `ValidateMagicToken` cek used-flag (H2-E3) | 06 |
| Konfigurasi & container | Secret bocor, dependency rentan | Secrets via env (`.env` di `.gitignore`, tidak ada default produksi hardcode); dependency/container scan pra-rilis (H6-E3) | 07 |
| JournalEntry, Assessment final | Tamper pasca-approve | `EnsureMutable()` domain guard — 409/403 tanpa jalur unlock diam-diam; unlock hanya prosedur ber-audit (fase 2) | 08 |
| SuperAdmin impersonation | Aksi tanpa jejak | `AuditLog.ActingAsUserId` — H6-E3, di luar scope H1 | 08 (parsial) |

## Catatan status H1 (gate M0)

- PKCE + lifetime token: **implemented** (`OpenIddictSetup.cs`), test lihat `Vokasia.Tests/Auth/`.
- Tenant filter: **stub aktif** (query filter mekanis hidup, `ITenantContext` masih diisi manual — middleware nyata H2-E3). Tidak ada gap keamanan baru dari stub ini karena belum ada endpoint publik yang membaca lintas tenant di H1.
- Rate limit, FluentValidation, EXIF-strip, impersonation audit: **belum dikerjakan** — dijadwalkan H2-E3/H3-E3/H4-E1/H6-E3 sesuai TICKETS.md. Bukan gap baru, sesuai rencana sprint.
- Dependency scan remediation: `Microsoft.OpenApi` is pinned to patched compatible 2.7.5; MailKit/MimeKit are aligned at 4.16.0; SSH.NET is explicitly overridden to 2026.0.0 for the test-container graph. The authoritative vulnerability scan remains a developer-machine verification gate.
