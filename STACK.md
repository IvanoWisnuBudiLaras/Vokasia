# STACK VOKASIA — Daftar Teknologi Lengkap

**Diverifikasi dari repo:** 31 Juli 2026
**Sumber:** `*.csproj`, `package.json`, `docker-compose*.yml`, `Dockerfile`, `skills-lock.json`, `TOOLING.md`
**Aturan:** semua versi di bawah dibaca langsung dari file konfigurasi, bukan dari ingatan.

---

## 1. BAHASA & RUNTIME

Backend: **C# / .NET 10 LTS** (`net10.0`)
Frontend: **TypeScript 5**
Runtime frontend: **Bun ≥ 1.2** (build) · **Node 22** (runtime produksi)
Markup & style: **HTML** · **CSS (OKLCH color space)**
Query: **SQL (PostgreSQL dialect)**
Script deploy: **Bash / PowerShell**

---

## 2. BACKEND — FRAMEWORK & PACKAGE

### Framework inti

Web framework: **ASP.NET Core 10** (Minimal API)
ORM: **Entity Framework Core 10**
Background worker: **.NET Generic Host** (`Microsoft.Extensions.Hosting` 10.0.10)

### Package `Vokasia.Api`

| Package | Versi | Fungsi |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Driver + provider EF Core untuk PostgreSQL |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.10 | Tooling migration |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.10 | Manajemen user, password hashing, role |
| `OpenIddict.AspNetCore` | 7.6.0 | OAuth2 / OIDC server |
| `OpenIddict.Server.AspNetCore` | 7.6.0 | Endpoint authorization & token |
| `OpenIddict.Validation.AspNetCore` | 7.6.0 | Validasi access token di API |
| `OpenIddict.EntityFrameworkCore` | 7.6.0 | Persistensi token & client OAuth |
| `MassTransit.RabbitMQ` | 8.5.10 | Abstraksi message bus |
| `Hangfire.AspNetCore` | 1.8.24 | Penjadwalan cron + dashboard |
| `Hangfire.PostgreSql` | 1.21.1 | Storage job di Postgres |
| `StackExchange.Redis` | 3.0.17 | Client Redis |
| `Minio` | 7.0.0 | Client S3-compatible object storage |
| `FluentValidation.DependencyInjectionExtensions` | 12.1.1 | Validasi input request |
| `Microsoft.AspNetCore.OpenApi` | 10.0.10 | Generasi spesifikasi OpenAPI |
| `Microsoft.OpenApi` | 2.7.5 | Model dokumen OpenAPI |

### Package `Vokasia.Worker`

| Package | Versi | Fungsi |
|---|---|---|
| `MassTransit.RabbitMQ` | 8.5.10 | Consumer antrian pesan |
| `Hangfire.AspNetCore` | 1.8.24 | Server eksekusi cron |
| `Hangfire.PostgreSql` | 1.21.1 | Storage job |
| `SixLabors.ImageSharp` | 3.1.11 | Kompresi foto, strip EXIF-GPS, thumbnail |
| `QuestPDF` | 2026.7.1 | Generasi sertifikat & rekap PDF |
| `Newtonsoft.Json` | 13.0.4 | Serialisasi payload event |
| `Microsoft.Extensions.Hosting` | 10.0.10 | Host background service |

> **Catatan versi ImageSharp:** dinaikkan 3.1.5 → 3.1.11 untuk menutup CVE-2025-27598 dan
> CVE-2025-54575 (decoder GIF). Relevan karena worker men-decode byte gambar yang diunggah
> user — input tidak tepercaya. Ini contoh bagus untuk disebut saat presentasi.

### Package `Vokasia.Infrastructure`

| Package | Versi | Fungsi |
|---|---|---|
| `Bogus` | 35.6.5 | Generator data seed demo (3 tenant, 900 siswa, 90 hari jurnal) |
| `MailKit` | 4.16.0 | Pengiriman email SMTP |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Provider database |
| `StackExchange.Redis` | 3.0.17 | Client Redis |
| `Minio` | 7.0.0 | Client object storage |
| `OpenIddict.EntityFrameworkCore` | 7.6.0 | Skema token OAuth |

### Package `Vokasia.Tests`

| Package | Versi | Fungsi |
|---|---|---|
| `xunit` | 2.9.3 | Test framework |
| `xunit.runner.visualstudio` | 3.1.4 | Test runner |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | SDK test .NET |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.10 | Integration test host in-process |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.10 | Provider DB in-memory untuk unit test |
| `Testcontainers.PostgreSql` | 4.13.0 | Postgres asli via Docker saat integration test |
| `Testcontainers.RabbitMq` | 4.13.0 | RabbitMQ asli via Docker saat integration test |
| `RabbitMQ.Client` | 7.2.1 | Inspeksi antrian & DLQ di test |
| `coverlet.collector` | 6.0.4 | Pengukuran code coverage |

---

## 3. FRONTEND — FRAMEWORK & PACKAGE

### Dependencies

| Package | Versi | Fungsi |
|---|---|---|
| `next` | 16.2.10 | Framework React — App Router, PPR, Server Components |
| `react` | 19.2.4 | Library UI |
| `react-dom` | 19.2.4 | Renderer DOM |
| `@tanstack/react-query` | ^5.101.3 | State server, caching, refetch |
| `zod` | ^4.4.3 | Validasi skema & parsing tipe-aman |
| `ioredis` | ^5.4.1 | Client Redis di sisi BFF (simpan token server-side) |

### DevDependencies

| Package | Versi | Fungsi |
|---|---|---|
| `typescript` | ^5 | Bahasa & type checker |
| `tailwindcss` | ^4 | Utility-first CSS |
| `@tailwindcss/postcss` | ^4 | Plugin PostCSS Tailwind v4 |
| `eslint` | ^9 | Linter |
| `eslint-config-next` | 16.2.10 | Aturan lint khusus Next.js |
| `@types/node` `@types/react` `@types/react-dom` | ^20 / ^19 / ^19 | Definisi tipe |

### Aset & desain

Font: **Geist** + **Geist Mono** (via `next/font`)
Warna: **OKLCH** — anchor hue 222° institusional, tema `sekolah` 213° scoped
Ikon & animasi: **tidak ada pustaka** (stance *motion-cut* — hemat payload di 3G)
PWA: **Web App Manifest** (`manifest.ts`) + halaman offline

---

## 4. DATABASE & INFRASTRUKTUR

Database: **PostgreSQL 17**
Cache & session store: **Redis 7**
Message broker: **RabbitMQ 3** (dengan management plugin)
Object storage: **MinIO** (S3-compatible)
Reverse proxy / TLS edge: **Caddy 2**
Orkestrasi: **Docker** + **Docker Compose** (7 service)

---

## 5. DOCKER IMAGE

| Image | Dipakai untuk |
|---|---|
| `postgres:17-alpine` | Database |
| `redis:7-alpine` | Cache & session |
| `rabbitmq:3-management-alpine` | Message broker + konsol manajemen |
| `minio/minio:latest` | Object storage |
| `caddy:2-alpine` | TLS edge / reverse proxy (profile `edge`) |
| `mcr.microsoft.com/dotnet/sdk:10.0` | Stage build backend |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | Runtime backend (api + worker) |
| `oven/bun:1` | Stage build frontend |
| `node:22-alpine` | Runtime frontend produksi |

> Multi-stage build: image SDK/Bun hanya dipakai saat compile, tidak ikut ke produksi.

---

## 6. POLA & ARSITEKTUR

| Pola | Diterapkan di |
|---|---|
| **Transactional Outbox** | Event tidak hilang saat broker mati |
| **BFF (Backend for Frontend)** | Token tidak pernah menyentuh browser |
| **Multi-tenant single-database** | Isolasi via EF Core global query filter |
| **RBAC dua lapis** | Policy API + route guard frontend |
| **Idempotent consumer** | Tabel `ProcessedMessages` + PK constraint |
| **Dead Letter Queue (DLQ)** | Pesan gagal tidak hilang diam-diam |
| **Event-driven** | 13 consumer MassTransit |
| **Magic link auth** | Mentor industri tanpa password |
| **PKCE (OAuth2)** | Wajib untuk semua authorization code flow |
| **Immutability** | Jurnal terkunci pasca-approve, nilai terkunci pasca-finalisasi |

---

## 7. TOOLING DEVELOPMENT

| Tool | Fungsi |
|---|---|
| **Git + GitHub (private repo)** | Versioning, rollback, worktree per agen |
| **VS Code + C# Dev Kit** | IDE utama — backend + frontend + Docker + git satu jendela |
| **Docker Desktop** | Runtime container lokal |
| **.NET SDK 10** | Build, test, migration EF |
| **Bun ≥ 1.2** | Package manager + build frontend |
| **EF Core CLI** | Generasi & apply migration (6 migration) |
| **ESLint 9** | Static analysis frontend |
| **Git worktree** | Isolasi kerja per agen AI (`.claude/worktrees/`) |

---

## 8. MCP SERVER (Model Context Protocol)

| MCP | Fungsi dalam proyek |
|---|---|
| **Desktop Commander** | Menjalankan `dotnet build` / `dotnet test` / git di mesin dev — verifikasi klaim test |
| **Windows-MCP** | Kontrol desktop Windows untuk operasi di luar terminal |
| **codebase-memory-mcp** | Knowledge graph repo: impact analysis & call-chain saat code review |
| **Context7** | Dokumentasi real-time .NET 10 / Next.js 16 / MassTransit — menekan API halusinasi |
| **PostgreSQL MCP** | Inspeksi skema & data saat review migration |
| **Playwright MCP** | Direncanakan untuk E2E 5 persona (**belum dieksekusi** — tiket H7-E3) |
| **Figma MCP** | Tersedia, **tidak dipakai di MVP** (keputusan: design-in-code, tanpa fase Figma) |

---

## 9. AGENT SKILLS (44 skill terkunci di `skills-lock.json`)

Skill = paket instruksi terstruktur yang mengubah cara AI mengerjakan satu jenis tugas.
Semua terkunci dengan hash — versi yang dipakai reproducible.

**Sumber:**

| Repositori | Jumlah |
|---|---|
| `mattpocock/skills` | 41 |
| `nutlope/hallmark` | 1 |
| `obra/superpowers` | 1 |
| `anthropics/skills` | 1 |

**Skill yang benar-benar membentuk hasil proyek ini:**

| Skill | Dampak nyata |
|---|---|
| **hallmark** (`nutlope/hallmark`) | Menghasilkan `DESIGN.md` — token OKLCH, pemilihan anchor hue 222°, verifikasi kontras WCAG programatik. Menemukan token border lama gagal kontras (1,3:1 vs syarat 3:1) |
| **code-review** | Format verdict berjenjang `[Blocker] [Critical] [Major] [Minor]` |
| **tdd** | Siklus merah→hijau wajib sebelum test dianggap sah |
| **verification-before-completion** | Larangan klaim "selesai" tanpa output eksekusi asli |
| **grilling / grill-me / batch-grill-me** | Stress-test rencana & desain sebelum dieksekusi |
| **to-tickets** | Pemecahan PRD → 21 tiket sprint dengan AC |
| **to-spec** | Perumusan spesifikasi dari kebutuhan mentah |
| **domain-modeling** · **ubiquitous-language** | Penamaan entity & istilah konsisten lintas kode dan dokumen |
| **codebase-design** · **improve-codebase-architecture** | Struktur 4 project backend + batas dependensi |
| **diagnosing-bugs** | Alur investigasi bug sistematis |
| **git-guardrails-claude-code** | Cegah `git add -A` yang menelan drift CRLF/LF |
| **handoff / claude-handoff** | Format serah-terima antar sesi (`HANDOFF-NEXT-SESSION.md`) |
| **webapp-testing** | Pola verifikasi frontend |
| **research** · **qa** · **triage** · **implement** | Alur kerja harian |

**Daftar lengkap 44 skill:**

`ask-matt` · `batch-grill-me` · `claude-handoff` · `code-review` · `codebase-design` ·
`design-an-interface` · `diagnosing-bugs` · `domain-modeling` · `edit-article` ·
`git-guardrails-claude-code` · `grill-me` · `grill-with-docs` · `grilling` · `hallmark` ·
`handoff` · `implement` · `improve-codebase-architecture` · `loop-me` · `migrate-to-shoehorn` ·
`obsidian-vault` · `prototype` · `qa` · `request-refactor-plan` · `research` ·
`resolving-merge-conflicts` · `scaffold-exercises` · `setup-matt-pocock-skills` ·
`setup-pre-commit` · `setup-ts-deep-modules` · `skills` · `tdd` · `teach` ·
`to-questionnaire` · `to-spec` · `to-tickets` · `triage` · `ubiquitous-language` ·
`verification-before-completion` · `wayfinder` · `webapp-testing` · `wizard` ·
`writing-beats` · `writing-fragments` · `writing-great-skills` · `writing-shape`

---

## 10. BANTUAN AI — PEMBAGIAN PERAN

Ini bukan "pakai ChatGPT buat nulis kode". Ini struktur tim dengan wilayah terkunci.

| Peran | Model | Wilayah | Batasan keras |
|---|---|---|---|
| **Developer** (manusia) | — | Semua | Satu-satunya yang boleh merge & deploy. Final approver |
| **VPM** — Product Manager + Lead Code Reviewer | **Claude** | Dokumen, tiket, review | **Dilarang menulis kode fitur.** Wajib menjalankan build/test sendiri untuk verifikasi |
| **ENG-1** — Backend | **ChatGPT / Codex** | `backend/` | **Dilarang menyentuh `frontend/`** |
| **ENG-2** — Frontend | **ChatGPT / Codex** | `frontend/` | **Dilarang menyentuh `backend/`**. `DESIGN.md` = kontrak mengikat |
| **ENG-3** — Auth, Security, QA | **ChatGPT / Codex** | Lintas project | Hanya lewat tiket eksplisit |

**Aturan bersama yang membuat ini jalan** (`SOUL.md`):

1. Kerjakan **hanya** task yang diberikan — ide di luar task ditulis sebagai catatan, tidak diimplementasi
2. Setiap asumsi ditandai `[ASSUMPTION]` di ringkasan
3. Setiap task selesai wajib menyertakan **output test asli**, bukan klaim
4. Maksimal 3 pertanyaan blocking per hari per engineer
5. Dilarang mengubah kontrak OpenAPI, skema DB, atau wilayah engineer lain tanpa tiket

**Hierarki kebenaran saat terjadi konflik:**
`PRD.md` → keputusan eksplisit Developer → penilaian profesional role.
Konflik antar level wajib ditandai eksplisit — dilarang memilih diam-diam.

**Antarmuka eksekusi:** Cowork (Claude desktop) + Codex (ChatGPT) + Desktop Commander,
dengan `git worktree` terpisah per agen untuk mencegah tumpang tindih.

---

## 11. API & LAYANAN EKSTERNAL

| Layanan | Status |
|---|---|
| **SMTP / Resend** | Pengiriman email keluar (undangan mentor, reminder, invoice) |
| **API wilayah emsifa** | Seed data provinsi/kota Indonesia |
| **API sekolah Dapodik / NPSN** | Seed data sekolah |
| **Midtrans** (payment) | ❌ Out-of-scope MVP — billing manual |
| **WhatsApp Business API** | ❌ Out-of-scope MVP |
| **SSO belajar.id** | ❌ Out-of-scope MVP |
| **Integrasi resmi Dapodik** | ❌ Out-of-scope MVP |

---

## 12. STANDAR & REGULASI YANG DIIKUTI

| Standar | Penerapan |
|---|---|
| **OAuth 2.0 + PKCE** (RFC 7636) | Wajib untuk semua authorization code flow |
| **OpenID Connect** | Lewat OpenIddict |
| **OpenAPI** | Source of truth kontrak API — perubahan lewat change control |
| **WCAG** | Kontras teks ≥ 4,5:1, batas UI ≥ 3:1 — diverifikasi programatik OKLCH→sRGB |
| **UU PDP (Perlindungan Data Pribadi)** | Field minimal, opt-in portofolio, strip EXIF-GPS, retensi foto 2 tahun, hak hapus saat lulus |
| **Kepmendikbudristek 262/M/2022** | Struktur periode PKL, rubrik penilaian default |
| **Kurikulum Merdeka — Panduan PKL** | Template rubrik (aspek teknis / softskill / kehadiran + bobot) |

---

## 13. RINGKASAN SATU SLIDE

> Kalau kamu cuma punya satu slide, pakai ini. Sisanya untuk menjawab pertanyaan.

```
Backend      : C# / .NET 10 LTS · ASP.NET Core Minimal API · EF Core 10
Frontend     : TypeScript 5 · Next.js 16 (App Router, PPR) · React 19 · Tailwind 4
Runtime      : Bun 1.2 (build) · Node 22 (produksi)
Database     : PostgreSQL 17
Cache/Session: Redis 7
Antrian      : RabbitMQ 3 + MassTransit 8
Penjadwalan  : Hangfire 1.8
File storage : MinIO (S3-compatible)
Auth         : OpenIddict 7.6 (OAuth2 + PKCE) + pola BFF + magic link
PDF          : QuestPDF
Gambar       : SixLabors.ImageSharp
Email        : MailKit (SMTP)
Validasi     : FluentValidation (backend) · Zod (frontend)
Test         : xUnit + Testcontainers (Postgres & RabbitMQ asli) — 86 file, 304 test
Deploy       : Docker Compose · 7 container · 1 VPS · Caddy 2 (TLS)
Tooling AI   : Claude (PM + code review) · ChatGPT/Codex ×3 (engineer)
               7 MCP server · 44 agent skill terkunci hash
```

---

## 14. CATATAN KEJUJURAN UNTUK PRESENTASI

Yang **ada di rencana tapi belum dieksekusi** — jangan diklaim sudah jalan:

- **Playwright E2E 5 persona** — tidak ada file `.spec.ts`, Playwright tidak terpasang di `package.json`. Tiket H7-E3, belum dikerjakan.
- **Figma** — MCP tersedia, tapi keputusan sadar: design-in-code tanpa fase Figma di MVP.
- **Caddy TLS edge** — `Caddyfile` sudah ditulis, tapi komentarnya sendiri menyatakan **belum diuji** (auditor tidak punya Docker saat itu). Uji dulu sebelum diklaim.
- **PostgreSQL MCP & Playwright MCP** — tercatat di rencana tooling; verifikasi sendiri apakah benar tersambung sebelum menyebutnya.

**Peringatan operasional:** `.claude/settings.json` berisi token API (sudah masuk
`.gitignore`, jadi aman dari repo). Tapi jangan pernah membuka file itu saat berbagi layar.
