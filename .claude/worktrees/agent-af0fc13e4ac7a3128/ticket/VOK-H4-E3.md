# VOK-H4-E3 — Idempotency & DLQ tests + email infrastructure + template

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-3 `backend/` | `h4-eng3-dlq-email` | GPT-5.4 Thinking | **extra high** | M3 | PRD FR-X-02/03 |

## Tugas

Membuktikan async backbone H4-E1 tahan banting (test duplicate/out-of-order/poison/DLQ) + infrastruktur email nyata dengan 5 template seragam.

## Implementasi

### 1. Test async — `Vokasia.Tests/Async/` (MassTransit TestHarness + Testcontainers)
- `DuplicateDeliveryTests` — tujuan: kirim message identik 2× ke tiap consumer kritis (JournalSubmitted, PhotoUploaded, JournalApproved, GhostingAlert-email) → efek tepat 1× (row count, notif count).
- `OutOfOrderTests` — tujuan: `JournalApproved` tiba sebelum `JournalSubmitted` terproyeksi → consumer tidak crash; status akhir konsisten (retry/requeue).
- `PoisonMessageTests` — tujuan: payload rusak/consumer throw permanen → retry sesuai policy (5×) → masuk `_error` queue (DLQ); message lain tetap mengalir.
- `DlqReplayTests` + script `tools/Replay-Dlq.ps1 -Queue <name> -Count <n>` — tujuan: replay dari DLQ ke queue asal; dipakai runbook & panel health SA (H6).
- `OutboxGuaranteeTests` — tujuan: kill broker di tengah publish → tidak ada event hilang/dobel setelah pulih.

### 2. Email — `Vokasia.Infrastructure/Email/`
- `IEmailSender.SendAsync(EmailMessage{To, TemplateId, ModelJson, NotificationId?})` — tujuan: kontrak tunggal; implementasi `SmtpEmailSender` (MailKit ke SMTP/Resend, config env) + `DevLogEmailSender` (env Development → tulis ke log/folder `.emails/`).
- Idempoten per `NotificationId` — tujuan: retry consumer tidak mengirim email dobel (cek tabel `SentEmail`).
- `EmailTemplateRenderer.Render(templateId, model) → {Subject, Html, Text}` — tujuan: base layout seragam (header Vokasia, footer, plain-text fallback) + 5 template:
  - `MentorInvite{MentorName?, StudentName, CompanyName, MagicLinkUrl, ExpiresAt}` — undangan magic link.
  - `JournalReminder{StudentName, Date}` — reminder 19:00.
  - `GhostingAlert{StudentName, CompanyName, EmptyDays, DashboardUrl}` — ke guru & admin.
  - `ExportReady{RequestedBy, DownloadUrl, ExpiresAt}` — hasil export H5.
  - `InvoiceIssued{SchoolName, Month, Amount, DueDate}` — billing H6.
- Wire ke consumer H4-E1: `MentorInvitedConsumer`, reminder, ghosting → kirim email nyata (dev: DevLog).

### 3. Konfigurasi retry/backoff final
- Review policy H4-E1 → satu sumber konstanta `MessagingDefaults` — tujuan: retry, redelivery, prefetch, nama queue & `_error` terdokumentasi di satu file.

## Acceptance Criteria

- Semua test §1 hijau, reproducible dari clean containers.
- Given consumer email di-retry, Then email terkirim 1× per notifikasi (bukti tabel `SentEmail`).
- Given render 5 template, Then layout konsisten + plain-text ada (snapshot test).
- Given poison message, Then DLQ terisi + `Replay-Dlq.ps1` mengembalikan & terproses.

## DoD + verifikasi runner (extra high)

Suite async 2× (kedua dari containers baru) → jalankan `Replay-Dlq.ps1` demo → tampilkan 5 email hasil render (folder `.emails/`) → setor.
