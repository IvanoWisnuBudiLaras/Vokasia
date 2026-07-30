# VOK-H4-E2 — Dashboard admin RAG (W3) + halaman guru bimbingan + notifikasi UI

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-2 `frontend/` | `h4-eng2-dashboard-rag` | GPT-5.3-Codex | medium | **M3** | PRD §4.3 W3, DESIGN.md |

## Tugas

Layar pemantauan: dashboard TenantAdmin sesuai W3 (KPI + siswa bermasalah RAG), halaman guru untuk siswa bimbingan, dan bell notifikasi in-app di semua shell.

## Implementasi

### 1. `/app` — Dashboard (W3)
- `app/page.tsx` — tujuan: konsumsi `GetSchoolDashboard(periodId)`; selector periode di header.
- `PeriodSelector({periods, value, onChange})` — tujuan: ganti periode aktif (persist di URL query).
- `KpiCards({journalTodayPct, pendingApprovals, lateVisits, flaggedCount})` — tujuan: 4 kartu W3; angka besar; flagged merah.
- `ProblemStudentList({items:[{name, companyName, rag, reason}]})` — tujuan: daftar 🔴🟡 terurut severity + alasan ("4 hr kosong", "ditolak 3×") + link detail siswa.
- `StudentDetailDrawer({studentId})` — tujuan: ringkasan siswa: status RAG, riwayat jurnal terakhir, placement, tombol lihat semua.

### 2. Halaman guru
- `app/bimbingan/page.tsx` — tujuan: khusus role Teacher: daftar siswa yang di-assign kepadanya + RAG + jurnal terbaru; sumber `ListPlacements(teacher scope)` + status.
- `JournalReviewList({placementId})` — tujuan: baca jurnal siswa + `AddTeacherComment` inline (FR-JRN-05); komentar tampil kronologis.

### 3. Notifikasi in-app (semua shell)
- `NotificationBell()` — tujuan: ikon + badge unread (poll ringan 60 dtk); klik → panel.
- `NotificationPanel({items, onMarkRead, onMarkAllRead})` — tujuan: daftar notif (`ListMyNotifications`), tipe ber-ikon (approve ✅, reject ✖, ghosting 🔴, reminder ⏰); klik item → navigasi kontekstual + `MarkRead`.

## Acceptance Criteria

- Given seed ghosting, When buka dashboard, Then siswa Red di atas dengan alasan; KPI sesuai data.
- Given guru login, Then hanya siswa bimbingannya (scope dari API — UI tidak memfilter sendiri).
- Given notif baru (approve dari mentor), Then badge bertambah tanpa reload penuh.
- `bun run build` hijau; desktop-first `/app`; state loading/empty/error lengkap.

## DoD + verifikasi runner (medium)

`bun run build` → smoke dashboard vs seed (angka cocok dengan query manual DB) → screenshot W3 untuk VPM → setor.
