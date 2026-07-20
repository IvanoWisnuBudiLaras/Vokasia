# VOK-H5-E2 — UI kunjungan (W4) + form nilai + rekap & finalisasi

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-2 `frontend/` | `h5-eng2-assessment-ui` | GPT-5.3-Codex | medium | M4 | PRD §4.3 W4, DESIGN.md |

## Tugas

UI rantai penilaian: guru mencatat kunjungan di HP (W4), mentor & guru mengisi rubrik, admin melihat rekap dan finalisasi, export 202.

## Implementasi

### 1. Kunjungan (mobile-first, role Teacher)
- `app/bimbingan/[placementId]/kunjungan/page.tsx` — tujuan: form W4: tanggal otomatis (editable), textarea catatan, foto lokasi (PhotoUploader reuse, max 1), area ttd.
- `SignaturePad({onChange(dataUrl)})` — tujuan: canvas gambar ttd pembimbing industri; clear/ulang; hasil dataURL ke API.
- `VisitHistoryList({placementId})` — tujuan: riwayat kunjungan (tanggal, cuplikan, foto, badge ttd ✓).

### 2. Form nilai
- `mentor/nilai/page.tsx` + `mentor/nilai/[placementId]/page.tsx` — tujuan: daftar siswa fase Assessment → form skor aspek industri.
- `ScoreForm({aspects:[{id, name, kind, weight}], values, onSave, readOnly})` — tujuan: input 0–100 per aspek (slider+angka), tampilkan bobot, autosave draft, dipakai mentor & guru; `readOnly` saat `IsFinal`.
- `app/penilaian/page.tsx` — tujuan: sisi guru: daftar siswa + status pengisian (mentor ✓/✗, guru ✓/✗) dari `GetAssessment`.

### 3. Rekap & finalisasi (TenantAdmin)
- `app/penilaian/rekap/page.tsx` — tujuan: tabel `GetGradeRecap` (nama, DUDI, skor mentor/guru, final, status); sort & cari.
- `FinalizeButton({periodId, incompleteCount})` — tujuan: konfirmasi dua langkah ("X siswa belum lengkap" bila ada, 422-aware); sukses → tabel terkunci + badge "Final".
- `ExportButton({periodId})` — tujuan: pilih Xlsx/Pdf → `RequestExport` 202 → toast "diproses, cek notifikasi" → notif `ExportReady` → link unduh.

## Acceptance Criteria

- Given guru di HP 360px, When isi kunjungan + ttd + foto, Then tersimpan & muncul di riwayat; ≤2 mnt.
- Given mentor isi 3 aspek lalu tutup, When kembali, Then draft tersisa (autosave).
- Given admin finalize sukses, Then semua ScoreForm jadi `readOnly` + rekap menampilkan final.
- Given export, Then tidak ada spinner blocking — 202 + notifikasi.

## DoD + verifikasi runner (medium)

`bun run build` → smoke alur: kunjungan → skor 2 sisi → finalize → export, terhadap API H5-E1 di compose → screenshot W4 + rekap → setor.
