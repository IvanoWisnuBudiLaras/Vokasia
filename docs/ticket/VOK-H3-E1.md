# VOK-H3-E1 — Journal endpoints + presigned upload + cron slot/reminder

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-1 `backend/` | `h3-eng1-journal-api-cron` | GPT-5.3-Codex | high | **M2** (core loop) | PRD FR-JRN-01..06 |

## Tugas

API lengkap siklus jurnal: slot harian (cron), isi jurnal + foto via presigned URL, approve/reject mentor (batch), komentar guru. Event ke outbox (consumer H4). Immutability guard dipasang H3-E3 — sediakan hook `EnsureJournalMutable` dipanggil di update path.

## Implementasi

### 1. Cron Hangfire — `Vokasia.Worker/Jobs/` (timezone `Asia/Jakarta` eksplisit)
- `GenerateDailyJournalSlots(DateOnly? runDate = null)` — 05:00 WIB. Tujuan: buat `JournalSlot` per placement aktif untuk `runDate`; **skip weekend + tabel Holiday**; idempoten (unique `PlacementId+Date`); param `runDate` untuk test/backfill.
- `RemindEmptyJournals()` — 19:00 WIB. Tujuan: siswa dengan slot hari ini `Empty` → `CreateNotification(JournalReminder)` + event email (H4).

### 2. Endpoints siswa — policy `StudentSelf`
- `GetTodayJournal() → TodayJournalDto{Slot, Entry?, Competencies[], WeekStatus[], Streak}` — tujuan: satu panggilan untuk layar W1 (hindari 4 fetch).
- `SubmitJournal(Guid slotId, SubmitJournalRequest{Text≤500, CompetencyIds[]≤5, PhotoIds[]≤3}) → JournalDto` — tujuan: isi jurnal slot milik sendiri; slot sudah terisi → 409; publish `JournalSubmitted` via outbox.
- `GetPresignedUploadUrl(UploadRequest{FileName, ContentType(image/jpeg|png|webp), SizeBytes≤5MB}) → {UploadUrl, ObjectKey, ExpiresIn}` — tujuan: upload langsung ke MinIO (API tidak menerima body file, NFR-SEC-06); key ber-prefix `tenant/{tid}/journal/`.
- `AttachPhoto(Guid journalId, string objectKey) → PhotoDto(Status=Pending)` — tujuan: daftarkan foto (maks 3); publish `PhotoUploaded` (processor H4).
- `ListJournals(JournalFilter{PlacementId?, Status?, From?, To?, Page}) → Paged<JournalDto>` — tujuan: riwayat; include foto thumbnail — **tanpa N+1** (projection).

### 3. Endpoints mentor — policy `MentorOwnPlacement`
- `GetPendingApprovals() → List<PendingDto>` — tujuan: jurnal Submitted lintas siswa bimbingannya, grup per siswa (layar W2).
- `ApproveJournal(Guid id, string? note)` — tujuan: Submitted→Approved + `ApprovedAt`; publish `JournalApproved`; panggil `EnsureJournalMutable` guard path.
- `RejectJournal(Guid id, string reason)` — tujuan: Submitted→Rejected + alasan wajib; siswa bisa isi ulang slot; publish `JournalRejected`.
- `BatchApprove(List<Guid> ids) → BatchResult{Approved[], Failed[{Id, Reason}]}` — tujuan: approve massal ≤2 mnt (NFR-UX-01); per item independen (satu gagal tak membatalkan lainnya); event per jurnal.

### 4. Endpoints guru — policy `Teacher+`
- `AddTeacherComment(Guid journalId, string text) → CommentDto` — tujuan: komentar pembinaan (FR-JRN-05); siswa ternotifikasi (event).
- `ListCompetencies(Guid majorId) → List<CompetencyDto>` — tujuan: daftar kompetensi per jurusan untuk form W1 (akses semua role tenant).

## Acceptance Criteria

- Given placement aktif + Senin kerja, When cron jalan (runDate injeksi), Then slot tercipta; Sabtu/libur kalender → tidak.
- Given teks 501 kar / foto ke-4 / slot orang lain, When submit, Then 400/403 presisi.
- Given 10 pending, When BatchApprove, Then semua Approved + 10 event outbox.
- Given `ListJournals` 90 hari, Then 1 query utama (log EF dibuktikan, tanpa N+1).

## DoD + verifikasi runner (high)

Build+test per kelompok (cron → siswa → mentor) → test cron dengan `runDate` → cek `SELECT count(*) FROM "OutboxMessage"` naik saat submit/approve → `git diff --stat` → setor.
