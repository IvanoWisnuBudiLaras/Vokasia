# VOK-H7-E1 — Perf pass + health checks + backup + README deploy

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-1 `backend/` + root | `h7-eng1-perf-ops` | GPT-5.3-Codex | high | **M6** (v0.1.0) | PRD NFR-PERF-02/03, NFR-REL-01/02, NFR-MNT-04 |

## Tugas

Hari ops: buktikan angka performa, health check menyeluruh, backup+restore teruji, dan README runbook agar VPS deploy & recovery bisa dilakukan siapa pun.

## Implementasi

### 1. Perf pass
- Audit query 5 endpoint tersibuk (`GetSchoolDashboard`, `ListJournals`, `GetPendingApprovals`, `GetGradeRecap`, `GetTodayJournal`) — tujuan: EXPLAIN, tambah index yang kurang (migration baru — aman dijalankan ulang), hapus N+1 tersisa.
- `tools/load/journal-burst.js` (k6) — tujuan: skenario NFR-PERF-03: 50 req/dtk submit jurnal 5 mnt (user seed) → error rate 0, queue menyerap; laporan `p95` per endpoint.
- `tools/load/read-p95.js` — tujuan: baca 5 endpoint → p95 <300ms; hasil ke `backend/docs/perf-H7.md`.

### 2. Health & ops
- `MapHealthChecks(app)` — tujuan: `/health/live` (proses) + `/health/ready` (Postgres, Redis, RabbitMQ, MinIO, Hangfire); compose healthcheck memakai ini; worker punya heartbeat sendiri.
- `docker-compose.prod.yml` — tujuan: overlay produksi: tanpa port debug, restart always, log rotation, resource limit wajar 1 VPS 4vCPU/8GB.

### 3. Backup
- `tools/backup/backup.ps1` + `backup.sh` — tujuan: `pg_dump -Fc` harian + MinIO mirror (`mc mirror`) → folder backup ber-tanggal; retensi 14 hari (hapus otomatis); exit code jelas untuk cron/Task Scheduler (NFR-REL-02).
- `tools/backup/restore.sh <dumpfile>` — tujuan: restore satu perintah ke DB kosong; **diuji nyata di ticket ini**.

### 4. README deploy — `README.md` (root)
- Tujuan: runbook lengkap: prasyarat VPS → clone → `.env` dari `.env.example` (tabel variabel + artinya) → `docker compose -f ... up -d` → migrate → seed → smoke check → jadwal backup → **prosedur restore <4 jam** (R8) → troubleshooting umum (DLQ replay, job gagal, disk penuh).

## Acceptance Criteria

- Given burst k6 50 req/dtk 5 mnt, Then error 0; laporan p95 dilampirkan; API p95 <300ms non-report.
- Given `docker compose down -v` → up → migrate → seed (1 perintah), Then app hidup end-to-end (NFR-MNT-04) — waktu dicatat.
- Given backup semalam, When `restore.sh` ke DB kosong, Then app hidup dengan data utuh (dibuktikan login + data cek).
- README bisa diikuti tanpa bertanya (uji: runner mengikuti README verbatim dari clean state).

## DoD + verifikasi runner (high)

Jalankan k6 kedua skenario + simpan laporan → uji restore nyata → ikuti README verbatim dari clean state (catat friksi → perbaiki README) → setor angka + laporan.
