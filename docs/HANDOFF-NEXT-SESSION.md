# HANDOFF — Lanjutan Sesi Vokasia (paska VOK-H4-E1 test phase)

Tempel file/pesan ini sebagai pesan PERTAMA di sesi Cowork baru. Kamu (Claude) melanjutkan eksekusi otonom sprint Vokasia yang sudah berjalan lintas banyak sesi — jangan minta konfirmasi ulang hal yang sudah diputuskan di bawah, dan JANGAN re-derive apa yang bisa langsung kamu baca dari file proyek.

## 0. Baca dulu, langsung dari file — jangan tanya ke user dulu

Root proyek: `D:\Web\Vokasia` (folder ini sudah ter-mount/terpilih).

1. `SOUL.md`, `AGENTS.md`, `TICKETS.md` — identitas, kontrak kerja, & daftar 21 ticket sprint.
2. `DECISIONS.md` — log keputusan D1–D26 kronologis. **Entry terakhir = D26** (VOK-H3-E2). Entry berikutnya yang HARUS kamu tulis nanti = **D27**, isinya gabungan: penutupan VOK-H4-E1 (test+verifikasi+DoD) DAN seluruh temuan handoff ini (blocker jaringan, temuan CRLF/LF, temuan teknis §5) — jangan dipecah jadi entry terpisah.
3. `ticket/VOK-H4-E1.md` — AC/DoD lengkap ticket yang sedang dikerjakan sekarang.
4. Panggil tool **TaskList** — task #1–42 sudah tercatat di sana (semua ticket H1–H3 + progres H4-E1). Jangan dibuat ulang, lanjutkan dari situ.

## 1. Instruksi baku yang berlaku (jangan tanya ulang ke user)

- Standing instruction: **kerjakan semua task sampai token habis**, otonom, tanpa menunggu konfirmasi per-ticket.
- Pola D14: sesi ini **coder + runner rangkap** (Sonnet 5, tanpa hand-off ChatGPT hidup — lingkungan non-interaktif). Ini bukan penyimpangan, sudah jadi pola baku sejak D14, ikuti saja.
- **PROMPT D wajib** untuk setiap test baru: rusak dulu implementasinya → konfirmasi test MERAH → kembalikan → konfirmasi HIJAU. Test tanpa siklus ini dianggap tak terbukti.
- **Setiap deviasi/temuan/gap didokumentasikan eksplisit di DECISIONS.md** — jangan diam-diam dilewati atau diam-diam diputuskan sepihak untuk hal yang sebenarnya wewenang Dev/user.
- Jangan klaim "sudah ditest" tanpa eksekusi nyata (lihat §3 — ini justru yang jadi alasan sesi lama berhenti).

## 2. Status sprint saat ini

Selesai (D14–D26, task #1–34): VOK-H1-E1/E2/E3, Gate M0, VOK-H2-E1/E2/E3, redesign dashboard (hallmark), VOK-H3-E1, VOK-H3-E3, VOK-H3-E2. Sedang dikerjakan: **VOK-H4-E1** (event-driven: outbox dispatcher, 7 consumer MassTransit, cron FlagGhostingStudents, dashboard sekolah, notifikasi in-app). Kode produksi (task #30–34) sudah selesai. Sedang di fase test+verifikasi (task #35, subtask #36–42):

- #36 IdempotencyGuard + consumer duplicate-delivery tests — **selesai**
- #37 FlagGhostingStudents cron tests — **selesai**
- #38 Dashboard endpoint tests — **selesai**
- #39 Notification endpoint tests — **kode SUDAH ditulis** (`backend/tests/Vokasia.Tests/Journal/NotificationEndpointsTests.cs`, 6 test: list-own-only, unreadOnly filter, mark-read own, **mark-read punya user lain → harus 404 bukan 403** [pola privasi disengaja, lihat komentar `NotificationEndpoints.MarkRead`], mark-read id tak ada → 404, mark-all-read scoped ke caller). **BELUM DIEKSEKUSI SAMA SEKALI** (lihat blocker §3) — belum PROMPT D, belum tahu hijau/merah.
- #40 `dotnet test` run pertama (PROMPT D merah/hijau) — **belum mulai**
- #41 Verifikasi live docker-compose (restart rabbitmq+worker, broker-down manual, cek DLQ kosong, cek log worker bersih) — **belum mulai**. Catatan: image `worker` di compose masih STALE (dibuat sebelum kode H4-E1 ditulis) — perlu `docker compose build worker` dulu.
- #42 Review `git diff`, commit, tulis DECISIONS.md D27 — **belum mulai**, lihat §4 soal bahaya `git add -A`.

Sisa sprint setelah H4-E1 tuntas (belum dibaca/dimulai sama sekali): VOK-H4-E2, H4-E3, H5-E1/E2/E3, H6-E1/E2/E3, H7-E1/E2/E3 (11 ticket, masing-masing di `ticket/VOK-H*.md`).

## 3. BLOCKER kritis — cek ini DULU sebelum nulis kode apa pun

Sesi sebelumnya (sandbox Cowork) **tidak punya dotnet SDK, tidak punya docker**, dan akses jaringan ke nuget.org + semua domain download Microsoft (dot.net, packages.microsoft.com, dotnetcli.azureedge.net, dotnetbuilds.azureedge.net, aka.ms, download.visualstudio.microsoft.com, download.microsoft.com, acquisition.dot.net) **diblokir proxy sandbox** — dikonfirmasi lewat header `X-Proxy-Error: blocked-by-allowlist` (disengaja, bukan flaky), sementara npm/PyPI/GitHub normal. Tidak ada root/sudo utk `apt-get install` pun. Ini sebabnya #39–#41 tak bisa dieksekusi sesi lalu, dan sebabnya sesi baru ini dibuat.

User sudah diarahkan ke **Settings → Capabilities → Code execution → "Allow network egress"** (claude.ai/settings/capabilities, akun individual Pro/Max) — perubahan itu HANYA berlaku utk sesi BARU (sesuai dokumentasi resmi Cowork), makanya sesi ini kemungkinan besar sengaja dibuat ulang.

**Langkah pertamamu di sesi ini**: jalankan smoke test murah dulu, JANGAN langsung asumsikan sudah beres:
```bash
dotnet --version
cd backend && dotnet restore
```
- Kalau **berhasil** → toolchain pulih, langsung lanjut ke §6 (lanjutkan #39→#42).
- Kalau **masih gagal** (dotnet not found, atau restore gagal krn nuget.org tetap ke-block) → **JANGAN ulangi investigasi jaringan dari nol** (sudah dicoba tuntas: dotnet-install.sh, apt Microsoft feed, ~10 domain mirror/CDN, apt Ubuntu native tanpa root, docker images cache, semua nihil). Langsung laporkan ke user bahwa toolchain masih belum jalan, dan tanya apakah settingnya sudah benar-benar diubah + sesi ini benar sesi baru.

## 4. State kerja: BELUM ADA YANG DI-COMMIT

Commit terakhir masih `a87ba40` (D26, H3-E2 frontend). **Seluruh kode H4-E1 (produksi + test) masih uncommitted di working tree.** File baru (untracked, semua punya H4-E1, aman di-`git add` semua):

```
backend/src/Vokasia.Domain/Events/
backend/src/Vokasia.Infrastructure/Messaging/
backend/src/Vokasia.Infrastructure/Migrations/20260721170357_AddTenantGeotagAllowed.*
backend/src/Vokasia.Worker/Consumers/
backend/src/Vokasia.Worker/Imaging/
backend/src/Vokasia.Api/Endpoints/DashboardEndpoints.cs
backend/src/Vokasia.Api/Endpoints/NotificationEndpoints.cs
backend/tests/Vokasia.Tests/Messaging/
backend/tests/Vokasia.Tests/Journal/DashboardEndpointsTests.cs
backend/tests/Vokasia.Tests/Journal/DashboardQueryCountVerification.cs
backend/tests/Vokasia.Tests/Journal/NotificationEndpointsTests.cs
```

Plus file **modified** yang genuinely H4-E1 (spot-checked nyata, bukan noise): `backend/src/Vokasia.Worker/Jobs/JournalCronJobs.cs` (FlagGhostingStudents), `backend/src/Vokasia.Worker/Vokasia.Worker.csproj` (bump SixLabors.ImageSharp 3.1.5→3.1.11, 2 CVE), `backend/tests/Vokasia.Tests/Journal/JournalCronJobsTests.cs`. Kemungkinan besar juga real (belum di-spot-check individual, cek dulu): `Vokasia.Api/Program.cs`, `Vokasia.Worker/Program.cs`, `Vokasia.Worker/Worker.cs`, `Vokasia.Api/Endpoints/Dtos.cs`, `Vokasia.Domain/Entities/{NotificationAndAuditEntities,OutboxEntities,TenantEntities}.cs`, `Vokasia.Domain/Common/Enums.cs`, `Vokasia.Infrastructure/DependencyInjection.cs`, ketiga `*.csproj` lain, `appsettings*.json` Api+Worker.

**PERINGATAN KERAS — JANGAN `git add -A` / `git commit -a`**: `git status` menunjukkan ~150 file lain "modified" (`.agents/skills/hallmark/**`, banyak file `frontend/**`, `backend/.idea/**`, migrations lama, dll) yang **BUKAN perubahan isi** — murni drift CRLF↔LF antara working tree dan HEAD (sudah diketahui & didokumentasikan sejak **D19**, masih belum diperbaiki, di luar wewenang sesi ini juga). Ciri pastinya: `git diff --stat` menunjukkan insertion == deletion == jumlah baris file itu persis (contoh dikonfirmasi ulang sesi ini: `SKILL.md` 558+/558-, `globals.css` 123+/123-). Sebelum staging apa pun di task #42: jalankan `git diff --stat -- <file>` per kandidat, kalau polanya "semua baris flip" → JANGAN di-add, itu bukan bagian dari H4-E1.

## 5. Temuan teknis penting (supaya tak diriset ulang / tak diulang salah)

- **MassTransit test harness** (`ITestHarness`): koleksi `Consumed`/`Sent`/`Published` cuma lacak pengiriman PERTAMA per `MessageId` — pengiriman ulang (redelivery) MessageId yang sama diam-diam tak masuk hitungan. Verifikasi duplicate-delivery harus baca state DB asli (mis. tabel `ProcessedMessages`, entity bisnis), bukan andalkan angka harness.
- **EF Core InMemory provider TIDAK menegakkan unique index** (`HasIndex(...).IsUnique()`) di bawah `SaveChangesAsync` konkuren dari 2 `DbContext` terpisah — bisa hasilkan baris duplikat palsu yang Postgres asli akan tolak (`DbUpdateException`, ditangani kode produksi `StudentDailyStatusUpsert.ApplyAsync` via retry). **Sudah dibuktikan BUKAN bug produksi** (30/30 run thd Postgres asli konvergen benar) — lihat `StudentDailyStatusUpsertConcurrencyTests.cs` (`[Fact(Skip=...)]`, catatan verifikasi manual 2026-07-22). **Jangan "perbaiki" `StudentDailyStatusUpsert` lagi** kalau nemu ini via InMemory.
- **`ProcessedMessage(ConsumerName, MessageId)` PK constraint** adalah lapis pertahanan KEDUA selain `IdempotencyGuard.EnsureNotProcessedAsync`'s `AnyAsync` check (satu `SaveChangesAsync` yang sama menyatukan marker+efek bisnis, jadi PK violation di marker ikut rollback efek bisnis). Kalau mau PROMPT-D-break guard: menonaktifkan `AnyAsync`-nya SAJA tidak cukup untuk merah — guard harus dibuat *true no-op* (tanpa `Add` sama sekali) untuk simulasikan "guard benar-benar absen".
- **`JournalCronJobsTests` pakai `IClassFixture<VokasiaApiFactory>`** → SEMUA `[Fact]` di kelas itu berbagi SATU DB InMemory. `FlagGhostingStudents()` sengaja TIDAK di-scope tenant (query global by design). Assertion baru di file ini WAJIB di-scope ke GUID unik test (filter by placementId/userId/dst di `PayloadJson` atau kolom FK), JANGAN pernah `Assert.Empty(db.SomeTable)` atau hitung whole-table — akan pecah begitu test lain menambah data ke fixture yang sama.
- `SixLabors.ImageSharp` di-bump 3.1.5→3.1.11 di `Vokasia.Worker.csproj` (CVE-2025-27598 & CVE-2025-54575, GIF decoder, relevan krn `PhotoUploadedConsumer` decode byte gambar tak terpercaya dari user).
- `PhotoProcessor.cs` (`Vokasia.Worker/Imaging/`) diekstrak jadi static class murni dari `PhotoUploadedConsumer` khusus supaya testable tanpa perlu fake `IMinioClient` (~70 member, terbukti lewat CS0535 probe).

## 6. Langkah selanjutnya (urutan pasti)

1. Jalankan smoke test §3. Kalau hijau, lanjut ke bawah.
2. Selesaikan #39: jalankan `NotificationEndpointsTests.cs`, minimal PROMPT D di 1 perilaku paling kritis (404-bukan-403 di `MarkRead` utk notifikasi user lain) — comment-out check kepemilikan → harus jadi 200 (merah) → kembalikan → 404 lagi (hijau).
3. #40: `dotnet test` full suite 2× berturut (bukan sekali lalu percaya).
4. #41: `docker compose build worker` (image stale) → `docker compose up -d` → restart rabbitmq+worker → `dotnet test` lagi → matikan rabbitmq manual, amati log worker tak crash → nyalakan lagi, konfirmasi reconnect+outbox jalan → cek DLQ/error queue kosong → cek log worker bersih.
5. #42: `git diff --stat` per file (lihat §4), stage HANYA yang H4-E1 nyata, commit, tulis DECISIONS.md **D27** (gabungkan: ringkasan H4-E1 lengkap + blocker jaringan §3 + temuan §5 + status CRLF/LF §4).
6. Lanjut VOK-H4-E2, lalu sisanya sesuai TICKETS.md — jangan berhenti minta konfirmasi tiap ticket kecuali benar-benar blocked (lihat pola §1).
