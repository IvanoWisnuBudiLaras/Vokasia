# VOK-H3-E2 — UI siswa isi jurnal (W1) + mentor batch approve (W2)

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-2 `frontend/` | `h3-eng2-journal-ui` | GPT-5.3-Codex | high | **M2** | PRD §4.3 W1–W2, NFR-UX-01/02, DESIGN.md |

## Tugas

Dua layar inti produk: siswa mengisi jurnal ≤2 menit di HP murah (W1), mentor batch-approve ≤2 menit (W2). Layout wireframe = kontrak. Semua state (loading/empty/error/offline) wajib.

## Implementasi

### 1. `/student` — Hari Ini (W1)
- `student/page.tsx` (Server Component) — tujuan: render `TodayJournalDto` dari `GetTodayJournal` — header (tanggal, perusahaan), presensi placeholder (fase 2), kartu jurnal, strip minggu + streak.
- `JournalForm({slot, competencies, onSubmitted})` (client) — tujuan: textarea ≤500 + counter live, pilih kompetensi (chips, maks 5), tombol **KIRIM JURNAL** besar (≥44px, `size:lg`); disable saat submit; sukses → optimistic update status hari.
- `PhotoUploader({max:3, onChange(photoIds)})` — tujuan: alur presigned: minta URL → `PUT` langsung MinIO → `AttachPhoto`; preview thumbnail lokal; progress per file; batal per file; error per file (retry) tanpa membatalkan form.
- `CompetencyPicker({options, selected, max:5})` — tujuan: bottom-sheet mobile, cari cepat.
- `WeekStrip({days:[{date, status:'done'|'pending'|'empty'|'holiday'}], streak})` — tujuan: ✅✅🟡⬜ + streak (motivasi — mitigasi R2).
- `student/history/page.tsx` + `JournalHistoryItem({journal})` — tujuan: riwayat berfilter status; badge Approved/Rejected + alasan.

### 2. `/mentor` — Approve mingguan (W2)
- `mentor/page.tsx` — tujuan: daftar `GetPendingApprovals` grup per siswa; header "Jurnal menunggu approval (n)".
- `ApprovalList({groups})` + `ApprovalCard({journal, expanded})` — tujuan: ringkas per jurnal (nama, sekolah, cuplikan teks, foto thumbnail); expand → teks penuh; ⚠ tanda siswa dengan hari kosong.
- `SelectAllBar({selectedIds, total, onApprove})` — tujuan: pilih semua/sebagian → `✔ APPROVE (n)` satu tap → `BatchApprove`; hasil parsial ditampilkan per item.
- `RejectDialog({journalId, onSubmit(reason)})` — tujuan: tolak wajib alasan; template alasan cepat (chips) agar ≤2 mnt.
- Approve/reject optimistic + rollback bila gagal.

### 3. Lintas
- Semua fetch via `fetcher` (BFF proxy) — tanpa akses token.
- Tiap page: skeleton loading, `EmptyState` ("Belum ada jurnal untuk di-approve 🎉"), `ErrorState onRetry`, `OfflineBanner`.
- Hanya komponen inti + tokens; copy Bahasa Indonesia sederhana.

## Acceptance Criteria

- Given HP 360px, When isi jurnal (teks+2 foto+2 kompetensi), Then selesai ≤2 mnt, ≤3 layar, tanpa zoom.
- Given mentor 8 pending, When pilih semua → approve, Then 1 konfirmasi → selesai; item gagal ditandai tanpa menggagalkan lainnya.
- Given koneksi diputus di kedua layar, Then `OfflineBanner` tampil, form tidak hilang isinya.
- Given `bun run build`, Then hijau; W1/W2 sesuai wireframe (screenshot dilampirkan runner).

## DoD + verifikasi runner (high)

`bun run build` → smoke di viewport 360px (screenshot W1 & W2 untuk VPM) → uji alur submit+approve terhadap API H3-E1 di compose → `git diff --stat` → setor.
