# AGENTS.md — Vokasia

Dibaca semua AI agent yang bekerja di repo ini. `PRD.md` = satu-satunya sumber requirement. `SOUL.md` = role & aturan main tim. `DESIGN.md` = kontrak visual (ENG-2). Konflik antar dokumen → tanya Developer, jangan memilih diam-diam.

## Konteks

SaaS multi-tenant manajemen PKL SMK. Dua project: `backend/` (.NET 10, sln `backend/Vokasia.sln`) + `frontend/` (Next.js 16 + Bun). Sprint 7 hari mulai 27 Jul 2026. Kontrak OpenAPI + skema DB dibekukan H1 — deviasi tanpa persetujuan = ditolak review.

## Perintah

```
# backend
cd backend && dotnet build && dotnet test
dotnet ef migrations add <Name> --project src/Vokasia.Infrastructure --startup-project src/Vokasia.Api

# frontend
cd frontend && bun install && bun dev        # build: bun run build

# infra
docker compose up -d postgres redis rabbitmq minio
```

## Aturan non-negotiable (dilanggar = REQUEST CHANGES)

1. **Tenant isolation**: EF Core global query filter `tenant_id` di semua entitas tenant-scoped; scope mentor difilter per **placement**, bukan tenant.
2. **RBAC** ditegakkan sebagai authorization policy di endpoint API (matrix PRD 2.3) — bukan hanya disembunyikan di UI.
3. **Auth**: JWT access 15 mnt + refresh rotation & reuse detection; token hanya di BFF/Redis; browser hanya httpOnly Secure SameSite=Lax cookie. **Dilarang token di localStorage/sessionStorage.**
4. **Input**: FluentValidation semua request; upload via presigned URL; EXIF-GPS di-strip (kecuali policy geotag tenant).
5. **Immutability**: JournalEntry immutable pasca-Approved; nilai final terkunci; unlock hanya via prosedur ber-audit.
6. **Event**: publish via transactional outbox; consumer MassTransit idempoten; retry + DLQ.
7. **Cron**: Hangfire, timezone `Asia/Jakarta` eksplisit.
8. **Test menyertai kode**: unit untuk logic, integration untuk endpoint kritis. Dilarang skip/hapus test agar hijau. Sertakan output test di setiap penyerahan.
9. **Migration** aman dijalankan ulang; index untuk query dashboard; hindari N+1.
10. **Next.js**: Server Components default; route guard di `proxy.ts`; tanpa secret/env server bocor ke client bundle.
11. **Data anak** (NFR-SEC-05): field minimal; portofolio publik opt-in tanpa kontak/NISN.
12. **Secrets** via env; dilarang commit `.env` atau secret hardcode.
13. **Stack terkunci** — dilarang tambah dependency/package baru tanpa persetujuan Developer.
14. Aksi sensitif → tulis ke AuditLog (FR-X-01).

## Git workflow

- Branch per task: `h{N}-eng{X}-{slug}` (contoh: `h3-eng1-journal-endpoints`).
- Commit kecil, pesan imperative English. Dilarang commit langsung ke `main`.
- Selesai task → push branch + ringkasan ≤10 baris + output test + daftar `[ASSUMPTION]` → review VPM → Developer merge.

## Bahasa

Kode, identifier, commit: English. Copy UI: Bahasa Indonesia sederhana (siswa SMK, mentor industri).
