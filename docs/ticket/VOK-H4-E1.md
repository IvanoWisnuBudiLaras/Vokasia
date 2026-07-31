# VOK-H4-E1 — MassTransit + transactional outbox + consumers + cron ghosting

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-1 `backend/` (fokus Worker) | `h4-eng1-outbox-consumers` | GPT-5.4 Thinking | **extra high** | **M3** (early warning) | PRD FR-X-02, FR-JRN-03/07, §2.4 |

## Tugas

Async backbone: outbox pattern (event tak pernah hilang), consumers idempoten di Worker, cron ghosting detection, notifikasi in-app, dan endpoint agregat dashboard sekolah (dipakai H4-E2).

## Implementasi

### 1. Outbox — `Vokasia.Infrastructure/Messaging/`
- `SaveToOutboxInterceptor : SaveChangesInterceptor` — tujuan: domain event dari aggregate → baris `OutboxMessage` DALAM transaksi yang sama dengan perubahan data (FR-X-02; broker down ≠ event hilang).
- `OutboxDispatcher : BackgroundService` — tujuan: poll batch 50 tiap 2 dtk, publish ke RabbitMQ via MassTransit, tandai `PublishedAt`; gagal → tetap unpublished (retry alami); urutan per aggregate terjaga (order by `OccurredAt`).
- `IdempotencyGuard.EnsureNotProcessed(consumerName, messageId) → bool` — tujuan: cek+insert `ProcessedMessage` (PK gabungan); duplicate delivery → skip senyap; dipanggil di AWAL setiap consumer.
- `AddVokasiaMassTransit(IServiceCollection s, cfg)` — tujuan: koneksi RabbitMQ, retry policy `5x exponential (1s→30s)`, `.UseDelayedRedelivery`, DLQ per queue (`*_error`), prefetch wajar.

### 2. Consumers — `Vokasia.Worker/Consumers/` (semua: idempoten via guard, satu tanggung jawab)
- `JournalSubmittedConsumer.Consume(ctx<JournalSubmitted{JournalId, StudentId, PeriodId, Date}>)` — tujuan: proyeksikan `StudentDailyStatus` (hari itu → Green) — dashboard tidak menghitung ulang.
- `StreakCounterConsumer.Consume(ctx<JournalSubmitted>)` — tujuan: hitung streak berjalan siswa (reset saat bolong hari kerja).
- `PhotoUploadedConsumer.Consume(ctx<PhotoUploaded{PhotoId, ObjectKey, TenantId}>)` — tujuan: unduh dari MinIO → kompres ≤200KB → **strip EXIF-GPS** (kecuali flag `GeotagAllowed` tenant) → thumbnail 320px → simpan `ThumbKey`, `Status=Processed`. Gagal decode → `Status=Failed` + notif siswa (bukan crash).
- `JournalApprovedConsumer.Consume(ctx<JournalApproved{JournalId, StudentId}>)` — tujuan: `CreateNotification(siswa, JournalApproved)` + proyeksi entri ke bahan portofolio (kompetensi terverifikasi++).
- `JournalRejectedConsumer.Consume(ctx<JournalRejected>)` — tujuan: notif siswa + alasan.
- `MentorInvitedConsumer.Consume(ctx<MentorInvited{InviteId, Email, StudentName, CompanyName}>)` — tujuan: render template undangan + kirim via `IEmailSender` (infra H4-E3; sementara interface + log-sender).
- `PlacementCreatedConsumer.Consume(ctx<PlacementCreated>)` — tujuan: welcome pack notifikasi siswa+guru.

### 3. Cron — `Vokasia.Worker/Jobs/`
- `FlagGhostingStudents()` — 21:00 WIB. Tujuan: per placement aktif: hitung **hari kerja** berurutan tanpa entry (skip Holiday+weekend); ≥3 → `StudentDailyStatus.Rag=Red` + `CreateNotification(guru & TenantAdmin, GhostingAlert{StudentName, Days})` + event email; 1–2 hari → Amber. Idempoten per hari (FR-JRN-07; target M3: notif sampai <1 mnt sejak cron).

### 4. Notifikasi & dashboard
- `CreateNotification(Guid userId, NotificationType type, object payload)` (`INotifier`) — tujuan: satu pintu notif in-app; email menyusul per type (H4-E3).
- `ListMyNotifications(Page, bool unreadOnly) → Paged<NotificationDto>` · `MarkRead(Guid id)` · `MarkAllRead()` — tujuan: endpoint bell FE.
- `GetSchoolDashboard(Guid periodId) → SchoolDashboardDto{JournalTodayPct, PendingApprovals, LateVisits, Flagged[{StudentId, Name, CompanyName, Rag, Reason}]}` — tujuan: SATU query agregat (proyeksi `StudentDailyStatus`) untuk layar W3 — p95 <300ms @900 siswa.

## Acceptance Criteria

- Given RabbitMQ dimatikan, When submit jurnal, Then data tersimpan + outbox unpublished; broker hidup → event terkirim (dibuktikan test/manual).
- Given message sama dikirim 2×, Then efek 1× (test duplicate delivery per consumer kritis).
- Given siswa seed ghosting (3 hari kerja kosong), When cron jalan, Then Red + notif guru&admin < 1 mnt.
- Given foto ber-GPS, Then hasil proses tanpa EXIF-GPS + thumbnail ada.
- Given dashboard @seed 900 siswa, Then 1 query utama, p95 <300ms.

## DoD + verifikasi runner (extra high)

Suite 2× (kedua: `docker compose restart rabbitmq worker` dulu) → uji broker-down manual → cek DLQ kosong pasca-suite + log worker bersih → `git diff --stat` → setor.
