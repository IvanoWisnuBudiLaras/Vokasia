# Vokasia V3 — Product Requirements Document
## Verified Learning Record untuk Perkembangan Siswa Selama PKL

**Status:** Draft siap implementasi / handoff engineering  
**Versi produk:** V3  
**Basis:** V2/V2.1 yang sudah feature-complete dan sedang melalui final integration/hardening  
**Dokumen visual:** `DESIGN.md` V2.1 tetap menjadi source of truth untuk visual grammar kecuali change-control resmi dibuat  

---

## 1. Ringkasan Eksekutif

Vokasia V2 menyelesaikan alur operasional PKL end-to-end: placement, jurnal, review Mentor, bimbingan Guru, assessment, reporting, administration, portfolio, verification, certificate, dan ATS CV.

Vokasia V3 tidak memperluas Vokasia menjadi LMS umum, job marketplace, atau platform kompetensi universal. V3 berfokus pada satu masalah:

> **Sekolah dan siswa perlu mengetahui bagaimana kemampuan siswa benar-benar berkembang selama satu periode PKL, berdasarkan penilaian Mentor Industri yang melihat pekerjaan siswa secara langsung.**

V3 menambahkan **Verified Learning Record**: rekam perkembangan kompetensi internal yang bersumber dari dua assessment formal Mentor selama satu placement PKL:

1. **Penilaian Tengah** — mulai waktunya pada titik tengah periode PKL.
2. **Penilaian Akhir** — mulai waktunya 7 hari sebelum `Placement.EndDate`.

Setiap placement menggunakan satu snapshot template penilaian DUDI yang sama untuk Penilaian Tengah dan Akhir. Mentor menilai kriteria kerja yang relevan bagi perusahaan, termasuk hard skill dan kompetensi profesional seperti komunikasi, teamwork, dan leadership bila digunakan DUDI.

V3 tidak menggabungkan hasil menjadi satu angka kompetensi global, tidak melakukan mapping kompetensi antarperusahaan, tidak membuat ranking, dan tidak menggunakan AI untuk menilai perkembangan.

---

# 2. Problem Statement

## 2.1 Masalah saat ini

Sistem PKL dapat mencatat aktivitas dan nilai, tetapi tanpa model perkembangan yang stabil hasilnya cenderung menjadi snapshot akhir:

- siswa mengetahui nilai, tetapi sulit melihat perubahan kemampuan dari pertengahan sampai akhir PKL;
- Guru mengetahui jurnal dan masalah operasional, tetapi tidak memiliki satu surface ringkas untuk melihat perkembangan yang dinilai Mentor;
- hasil assessment dapat kehilangan konteks pekerjaan/evidence yang mendukungnya;
- histori PKL siswa tersebar per placement dan belum terbaca sebagai rekam perkembangan yang konsisten;
- data dalam jumlah besar berisiko membuat laporan berat di browser jika seluruh dataset dikirim sekaligus.

## 2.2 Masalah yang V3 selesaikan

V3 harus membuat Vokasia mampu menjawab:

1. Kompetensi apa yang dinilai pada siswa selama PKL ini?
2. Seberapa baik siswa melakukannya menurut Mentor Industri?
3. Bagaimana hasilnya berubah dari Penilaian Tengah ke Penilaian Akhir?
4. Evidence pekerjaan apa yang dikonfirmasi Mentor sebagai pendukung assessment?
5. Apakah Guru melihat adanya masalah perkembangan yang perlu ditindaklanjuti?
6. Bagaimana sekolah melihat dan mengekspor perkembangan siswa tanpa membebani browser dengan seluruh dataset?

---

# 3. Product Positioning

## 3.1 Posisi V3

**V2:** PKL management end-to-end.  
**V3:** Verified Learning Record selama PKL.  

V3 tetap menjadikan **Placement/PKL resmi** sebagai unit utama. V3 bukan sistem untuk lomba, freelance, kursus, proyek pribadi, atau pengalaman yang dimasukkan siswa sendiri.

## 3.2 Visi jangka panjang

Learning Record V3 harus cukup rapi untuk menjadi trust foundation bila Vokasia kelak berkembang menjadi platform siswa menuju industri. Namun V3 **tidak** mengimplementasikan job marketplace, matching, screening CV, atau recruitment workflow.

---

# 4. Goals

V3 wajib:

1. Menyediakan rekam perkembangan kompetensi per Placement.
2. Menyimpan dua observation formal: Tengah dan Akhir.
3. Menjaga provenance: siapa menilai, kapan, berdasarkan template apa, dan evidence apa.
4. Menjaga histori assessment tetap stabil walau template DUDI berubah kemudian.
5. Memisahkan assessment Mentor dari monitoring Guru.
6. Menyediakan halaman internal **Perkembangan** untuk siswa dan role berwenang.
7. Menyediakan laporan perkembangan yang scalable dengan server-side pagination/filter/sort.
8. Menyediakan PDF/XLSX untuk genuine reporting surfaces dengan scope export yang dapat diatur.
9. Menjaga authorization, tenant isolation, audit trail, dan historical integrity.
10. Tetap sederhana dan deterministic untuk implementasi dan testing.

---

# 5. Non-Goals / Explicit Exclusions

V3 **tidak** mengimplementasikan:

- universal/canonical competency taxonomy;
- automatic competency mapping antar-DUDI;
- fuzzy matching nama kompetensi;
- AI competency matching;
- AI recommendation;
- AI-generated scoring;
- ranking siswa;
- leaderboard/gamification/streak;
- competency percentage global;
- readiness/employment prediction;
- DUDI quality score;
- public Learning Record;
- selective public sharing Learning Record;
- free-form checkpoint assessment;
- monthly/weekly assessment engine;
- weighting antarcriterion untuk Learning Record;
- satu nilai gabungan Mentor + Guru;
- Teacher competency scoring;
- Student-created experience di luar Placement;
- automatic sync Learning Record ke Portfolio;
- Portfolio/CV customization;
- job marketplace / JobStreet/Glints-like recruitment flow.

---

# 6. Product Principles

## 6.1 Fakta lebih penting daripada inferensi

Vokasia menyimpan dan menampilkan assessment yang benar-benar dilakukan Mentor. Sistem tidak menyimpulkan bahwa dua kompetensi dengan nama mirip adalah kompetensi yang sama.

## 6.2 Provenance tidak boleh hilang

Setiap hasil harus dapat dilacak ke:

- Placement;
- assessment stage;
- Mentor evaluator;
- template snapshot;
- criterion snapshot;
- score;
- komentar;
- evidence terpilih, bila ada;
- waktu finalized.

## 6.3 Perkembangan ≠ satu nilai akhir

Tengah dan Akhir menjadi dua titik formal yang menentukan perkembangan. V3 tidak menghitung rata-rata baru untuk menyembunyikan perubahan tersebut.

Contoh yang benar:

- Tengah: `3 — Cukup`
- Akhir: `5 — Sangat Baik`

Bukan:

- “Nilai perkembangan: 4”.

## 6.4 Guru melakukan judgment manusia

Vokasia menyediakan konteks. Guru menentukan apakah ada masalah dan tindakan yang perlu dilakukan. Sistem tidak otomatis mengklasifikasikan siswa bermasalah hanya karena nilai rendah atau turun.

## 6.5 Browser menerima data seperlunya

Hak akses terhadap ribuan record tidak berarti browser harus memuat ribuan record sekaligus.

---

# 7. Personas dan Responsibility Model

## 7.1 Student

Student dapat:

- melihat Learning Record miliknya;
- melihat hasil Tengah setelah finalized;
- melihat hasil Akhir setelah finalized;
- melihat detail perkembangan per criterion;
- melihat komentar Mentor sesuai assessment;
- melihat evidence pendukung yang dapat diakses;
- melihat riwayat Placement lama dalam scope account-nya.

Student tidak dapat:

- mengubah score;
- memilih evidence assessment;
- mengubah template;
- reopen assessment;
- membuat Learning Record public.

## 7.2 IndustryMentor

Mentor adalah **primary workplace evaluator**.

Mentor dapat:

- membuat/mengelola template penilaian DUDI dalam scope DUDI-nya;
- menilai hard skill;
- menilai communication;
- menilai teamwork;
- menilai leadership;
- menilai problem solving/work behavior;
- menambah criterion khusus DUDI;
- melakukan Penilaian Tengah;
- melakukan Penilaian Akhir;
- melihat hasil Tengah saat mengisi Akhir;
- memberi komentar criterion opsional;
- memberi overall note wajib;
- memilih Approved journal/evidence sebagai bukti criterion;
- finalize assessment.

Mentor tidak dapat:

- reopen assessment finalized;
- mengubah assessment tenant/DUDI lain;
- mengubah Teacher monitoring;
- mengakses Learning Record placement yang tidak dibimbingnya.

## 7.3 Teacher

Guru adalah **monitor perkembangan dan exception handler**, bukan competency scorer.

Guru dapat:

- membaca Learning Record siswa yang berada dalam scope bimbingannya;
- melihat score dan progression Mentor;
- melihat evidence yang dipilih Mentor;
- melihat assessment yang overdue;
- membuat monitoring note ketika dibutuhkan;
- memilih status monitoring;
- melakukan bimbingan/intervensi melalui workflow yang sudah ada.

Guru tidak dapat:

- memberi competency score V3;
- mengubah assessment Mentor;
- reopen assessment;
- melihat monitoring internal Guru lain di luar scope.

## 7.4 TenantAdmin

TenantAdmin dapat:

- melihat learning/reporting data dalam tenant;
- melakukan governance terhadap DUDI/placement;
- reopen assessment finalized dengan alasan wajib;
- melihat audit trail;
- mengakses report/export sesuai authorization.

TenantAdmin tidak menggantikan Mentor sebagai evaluator kerja.

---

# 8. Core Domain Model

## 8.1 Placement sebagai unit utama

Learning Record V3 hanya berasal dari `Placement` PKL resmi.

Satu siswa dapat memiliki beberapa Placement sepanjang menggunakan Vokasia. Halaman Perkembangan mengurutkan Placement terbaru lebih dahulu.

Tidak ada abstraction `Experience` umum di V3.

## 8.2 Assessment stage

Stage hanya:

```text
Middle
Final
```

Tidak ada free checkpoint stage.

## 8.3 Score scale global

Semua criterion menggunakan skala Vokasia yang sama:

| Score | Label |
|---:|---|
| 1 | Sangat Kurang |
| 2 | Kurang |
| 3 | Cukup |
| 4 | Baik |
| 5 | Sangat Baik |

Database menyimpan nilai numerik 1–5 sebagai source of truth.

String label adalah presentation mapping.

Nilai <1 atau >5 ditolak server-side.

Tidak ada 0–100, star score, percentage proficiency, atau normalization antar-DUDI.

---

# 9. DUDI Assessment Template

## 9.1 Ownership

Template assessment dimiliki oleh DUDI.

IndustryMentor yang aktif dan terhubung ke DUDI dapat mengelola template DUDI tersebut sesuai authorization yang ditetapkan backend.

Perubahan template wajib diaudit.

TenantAdmin dapat melakukan governance administratif, tetapi tidak menjadi pihak yang wajib menerjemahkan substansi teknis DUDI.

## 9.2 Default criterion

Template baru dapat menyediakan default suggested criteria:

- Communication / Komunikasi
- Teamwork / Kerja sama tim
- Leadership / Kepemimpinan

DUDI **boleh menghapus** criterion default yang tidak relevan.

DUDI bebas menambah criterion lain.

Tidak ada universal ontology.

## 9.3 Jumlah criterion

Rekomendasi UX:

- 5–15 criterion;
- hard safety limit: 20 criterion per template version.

Tidak ada requirement jumlah tepat.

## 9.4 Weight

V3 Learning Record tidak memakai bobot criterion.

Tidak ada weighted overall score.

## 9.5 Versioning

Template yang sudah digunakan tidak boleh dimutasi sehingga mengubah arti assessment historis.

Model yang diharapkan:

- draft template dapat diedit;
- saat perubahan diperlukan setelah template aktif/dipakai, sistem membuat versi baru;
- Placement baru dapat memakai versi terbaru;
- Placement lama tetap terikat pada snapshot/version yang semula dipakai.

---

# 10. Placement Template Snapshot

## 10.1 Rule utama

> **Satu Placement = satu assessment template snapshot untuk Middle dan Final.**

Penilaian Tengah dan Akhir harus membandingkan criterion yang sama.

## 10.2 Contoh

```text
Placement A — 2027
Template Snapshot V1
├── Middle → V1
└── Final  → V1

DUDI mengubah template menjadi V2

Placement B — 2028
├── Middle → V2
└── Final  → V2
```

Template V2 tidak mengubah Placement A.

## 10.3 Snapshot minimal

Snapshot harus mempertahankan minimal:

- criterion name;
- criterion description/rubric;
- sort order;
- template/version identity;
- relevant display/provenance fields.

---

# 11. Assessment Lifecycle

## 11.1 Middle assessment due point

Titik Tengah dihitung secara deterministic dari `Placement.StartDate` dan `Placement.EndDate` menggunakan date semantics project yang sudah ada.

**V3 tidak memperkenalkan subsystem timezone baru.**

Timezone kompleks/deployment multi-zone ditunda. Gunakan existing project date semantics secara konsisten.

Status Middle:

- sebelum midpoint: `Belum waktunya`;
- midpoint tercapai dan belum finalized: `Perlu diisi`;
- midpoint lewat dan belum finalized: `Tertunda`;
- finalized: `Selesai`.

Midpoint adalah due point, bukan one-day submission window.

## 11.2 Final assessment due point

Final mulai tersedia:

```text
Placement.EndDate - 7 hari
```

Status Final:

- sebelum window: `Belum waktunya`;
- mulai H-7: `Perlu diisi`;
- EndDate lewat dan belum finalized: `Tertunda`;
- finalized: `Selesai`.

## 11.3 Middle belum selesai saat Final tersedia

Rekomendasi final:

- Middle tetap `Tertunda`;
- Final tetap dapat tersedia sesuai jadwal;
- Mentor melihat warning bahwa Middle belum selesai;
- sistem tidak otomatis menyalin score;
- kedua stage tetap harus difinalize secara independen.

Alasan: memblokir Final karena Middle lupa diisi dapat membuat data akhir ikut hilang.

## 11.4 Mentor melihat Middle ketika mengisi Final

Mentor **boleh melihat score dan catatan Middle** saat mengisi Final.

Tujuannya membantu evaluasi perkembangan sadar, bukan blinded assessment.

Tidak ada auto-copy.

---

# 12. Assessment Form

## 12.1 Per criterion

Setiap criterion mempunyai:

- criterion name;
- rubric/description;
- score 1–5 **wajib**;
- comment **opsional**;
- supporting evidence **opsional**.

## 12.2 Overall note

Setiap Middle dan Final assessment membutuhkan satu **overall note wajib**.

Contoh limit produk:

- minimum bermakna: ±10 karakter;
- hard maximum: ±1500 karakter.

Exact implementation limit boleh disesuaikan engineering selama tidak menjadi unlimited payload.

## 12.3 Finalization

Assessment draft editable sampai Mentor menekan Finalize/Submit.

Finalize harus:

- validate seluruh score;
- validate overall note;
- validate evidence ownership;
- snapshot provenance;
- menjadi idempotent terhadap retry/double-submit;
- menghasilkan audit event.

---

# 13. Evidence pada Assessment

## 13.1 Evidence opsional

Evidence bukan syarat score valid.

Communication, Teamwork, Leadership, atau kompetensi observasional lain boleh dinilai berdasarkan observasi langsung Mentor tanpa file.

## 13.2 Evidence source

Mentor hanya boleh memilih evidence dari **jurnal berstatus Approved**.

Tidak boleh dari:

- Draft;
- Menunggu Review;
- Perlu Revisi.

## 13.3 Evidence per criterion

Evidence dipilih per criterion, bukan satu bucket global assessment.

Contoh:

```text
REST API
4 — Baik
Evidence:
- Jurnal 12 Mei — endpoint transaksi
- Evidence 16 Mei — pengujian request

Teamwork
4 — Baik
Evidence:
- optional / none
```

## 13.4 Evidence validation

Server harus memastikan:

```text
Evidence.StudentId == Assessment.StudentId
AND Evidence.PlacementId == Assessment.PlacementId
AND Journal.Status == Approved
AND Evidence dapat diakses oleh evaluator
```

Mengganti ID request tidak boleh memungkinkan reference ke evidence siswa/tenant lain.

## 13.5 Reuse evidence

Evidence yang digunakan di Middle boleh digunakan kembali di Final.

---

# 14. Finalized Assessment & Reopen

## 14.1 Locking

Assessment finalized menjadi read-only.

Mentor tidak dapat mengedit langsung.

Teacher tidak dapat mengedit atau reopen.

## 14.2 Reopen authority

Hanya TenantAdmin dalam tenant yang sama yang dapat reopen.

Reopen membutuhkan alasan non-empty.

Audit minimal menyimpan:

- assessment;
- actor;
- timestamp;
- reason;
- stage;
- tenant.

## 14.3 Reopen UX semantics

Rekomendasi final:

- Learning Record tetap menampilkan **last finalized result** selama assessment sedang reopened;
- tampilkan status `Sedang diperbaiki` pada stage terkait;
- draft perubahan tidak menggantikan hasil yang sebelumnya finalized;
- setelah Mentor finalize ulang, Learning Record berpindah ke finalized revision terbaru.

Ini mencegah Learning Record tiba-tiba kosong atau menampilkan draft belum sah.

## 14.4 Audit vs full UI version history

V3 tidak membutuhkan full visual diff/version browser untuk setiap perubahan score.

Audit harus cukup untuk compliance dan debugging; UI hanya menampilkan state yang relevan bagi pengguna.

---

# 15. Mentor Replacement Edge Case

Jika Mentor diganti di tengah Placement:

- Middle finalized oleh Mentor lama tetap mencatat Mentor lama sebagai evaluator;
- Final dapat dilakukan Mentor baru jika assignment sah;
- Learning Record menampilkan provenance masing-masing stage;
- assessment finalized lama tidak dipindahkan seolah dibuat Mentor baru;
- DUDI template snapshot tetap sama.

Jika Middle masih draft saat Mentor diganti, ownership draft mengikuti aturan backend yang paling aman; rekomendasi: draft lama tidak otomatis dianggap submission Mentor baru. Mentor baru memulai/mengambil assessment sesuai explicit transition yang diaudit.

---

# 16. Placement Date Change Edge Case

Jika StartDate/EndDate berubah:

- due-state untuk stage **yang belum finalized** dihitung ulang dari periode terbaru;
- assessment yang sudah finalized tidak dihapus atau diubah;
- FinalizedAt dan stage provenance tetap historis;
- perubahan periode wajib mengikuti audit/authorization Placement existing.

Tidak ada timezone subsystem baru di V3.

---

# 17. Student Learning Record — `Perkembangan`

## 17.1 Surface baru

Student mendapatkan surface/menu **Perkembangan**.

Navigation placement final mengikuti batas mobile nav existing; V3 tidak memaksa >5 bottom navigation items jika merusak UX.

## 17.2 Empty/process state sebelum Middle

Menu tetap ada sejak awal PKL.

Sebelum Middle finalized:

> Penilaian Tengah belum tersedia. Perkembangan kompetensi akan muncul setelah Mentor menyelesaikan Penilaian Tengah.

Jangan menampilkan daftar criterion kosong sebagai pseudo-assessment.

## 17.3 Default display setelah Middle

Setelah Middle finalized, setiap criterion menampilkan Middle sebagai current/latest result.

## 17.4 Default display setelah Final

Setelah Final finalized, setiap criterion menampilkan Final sebagai latest result.

Contoh:

```text
Membuat REST API
5 — Sangat Baik
Penilaian Akhir

[Lihat perkembangan]
```

## 17.5 Detail perkembangan

`Lihat perkembangan` menampilkan dua titik formal, tanpa grafik wajib:

```text
Penilaian Tengah
3 — Cukup
Mentor: A

Penilaian Akhir
5 — Sangat Baik
Mentor: B
```

Komentar criterion tampil di detail, bukan memenuhi overview.

Evidence tampil bila ada.

## 17.6 Grouping

Learning Record dikelompokkan berdasarkan Placement/PKL.

Placement terbaru di atas.

Tidak ada automatic merge antar-DUDI.

---

# 18. Teacher Monitoring

## 18.1 Teacher tidak memberi competency score

Tidak ada Teacher Middle/Final scoring V3.

Teacher membaca:

- progression Mentor;
- journal activity;
- revision state;
- Mentor overall note;
- selected evidence;
- assessment overdue;
- guidance/intervention history existing.

## 18.2 Monitoring status

Jika Guru perlu mencatat kondisi, Guru memilih manual:

- `Berkembang sesuai harapan`
- `Perlu perhatian`
- `Bermasalah`

Sistem tidak otomatis menentukan status berdasarkan score.

## 18.3 Monitoring tidak wajib periodik

Guru tidak dipaksa membuat form monitoring setiap minggu/bulan atau setelah setiap assessment.

Monitoring dibuat ketika Guru memang perlu mencatat hasil analisis/tindak lanjut.

## 18.4 Reason requirement

Jika Guru memilih:

- `Perlu perhatian`
- `Bermasalah`

catatan/alasan wajib.

`Berkembang sesuai harapan` dapat menggunakan catatan opsional.

## 18.5 Visibility

Setiap catatan monitoring memiliki visibility:

- `Terlihat siswa`
- `Internal`

Mentor tidak melihat monitoring Guru.

## 18.6 Timeline

Monitoring disimpan sebagai event/timeline, bukan overwrite satu field historis.

UI dapat menampilkan latest status secara ringkas, tetapi histori monitoring tetap tersedia bagi role yang berwenang.

## 18.7 Resolution

Rekomendasi final untuk V3:

- monitoring event tidak memerlukan workflow ticket kompleks;
- Guru dapat menambahkan follow-up event baru;
- issue dianggap selesai melalui follow-up/status berikutnya, bukan destructive edit histori;
- existing guidance/intervention flow tetap menjadi tempat tindakan operasional.

---

# 19. Reminder & Exception Handling

## 19.1 Mentor reminder

Mentor mendapatkan:

- in-app notification;
- email notification.

Cadence:

1. sekali saat assessment menjadi `Perlu diisi`;
2. sekali saat assessment menjadi `Tertunda`.

Tidak ada reminder harian tanpa henti.

## 19.2 Teacher exception

Guru melihat assessment Mentor yang overdue sebagai exception pada workspace Teacher.

Guru tidak mengambil alih score.

## 19.3 Notification idempotency

Worker/job harus menggunakan idempotency/dedup key berbasis minimal:

```text
PlacementId
+ AssessmentStage
+ ReminderType(Due|Overdue)
+ Recipient
```

Retry worker tidak boleh mengirim email/notifikasi yang sama berulang.

---

# 20. Historical Identity

Learning Record historis tidak boleh bergantung penuh pada mutable display data saat ini.

Data yang perlu dipertahankan/snapshot bila relevan:

- DUDI name at placement context;
- evaluator display name/identity reference;
- criterion name/rubric;
- template version;
- assessment stage;
- score;
- comment;
- overall note;
- FinalizedAt;
- evidence references.

Renaming DUDI atau Mentor account tidak boleh membuat record lama tidak dapat dimengerti.

Implementasi boleh mempertahankan stable foreign key + historical display snapshot sesuai pattern domain existing.

---

# 21. Privacy & Authorization

Learning Record V3 adalah **internal/private**.

Tidak ada anonymous endpoint.

## 21.1 Permission matrix

| Capability | Student | Mentor | Teacher | TenantAdmin |
|---|---|---|---|---|
| Read own Learning Record | Yes | N/A | N/A | N/A |
| Read assigned student record | No | Placement sendiri | Scope bimbingan | Tenant |
| Create/edit assessment draft | No | Assigned placement | No | No |
| Finalize Mentor assessment | No | Yes | No | No |
| Reopen finalized assessment | No | No | No | Yes |
| Read selected evidence | Own | Assigned placement | Scope bimbingan | Tenant scope |
| Manage DUDI assessment template | No | DUDI sendiri | No | Governance only |
| Create Teacher monitoring | No | No | Yes | sesuai existing admin capability bila diperlukan |
| Read internal Teacher note | No | No | Authorized Teacher | TenantAdmin sesuai governance |
| Export multi-student report | No/default personal only | No unless defined | Scoped | Tenant |

Backend authorization adalah source of truth. UI hiding bukan security boundary.

## 21.2 Tenant isolation

Semua mutation/query internal wajib tenant scoped.

Cross-tenant ID substitution harus ditolak.

---

# 22. Large Data UX Contract

## 22.1 Core rule

> Browser tidak fetch seluruh dataset besar secara default.

Dataset berpotensi besar wajib menggunakan:

- server-side pagination;
- server-side search;
- server-side filter;
- server-side sort.

## 22.2 Pagination defaults

Rekomendasi:

- mobile/general: 20–25 rows;
- desktop operational table: 25 rows;
- dense reporting/admin: 50 rows;
- selectable page size: `25 / 50 / 100`.

Tidak ada `load all` untuk browsing.

## 22.3 Pagination UX

Gunakan pagination klasik untuk laporan/table:

```text
Menampilkan 26–50 dari 315
[← Sebelumnya]   Halaman 2 dari 13   [Berikutnya →]
```

Mobile:

```text
26–50 dari 315
[←]  2 / 13  [→]
```

`Load more` hanya untuk timeline/feed-like surface, bukan operational table/report.

## 22.4 URL query state

Filter/search/sort/page sebaiknya dipertahankan dalam URL ketika berguna:

```text
?period=...&status=...&page=2&pageSize=25
```

Filter berubah → page reset ke 1.

## 22.5 Small bounded data

Jangan tampilkan pagination jika dataset memang kecil, misalnya Middle + Final history yang hanya dua item.

---

# 23. Evidence Performance

Evidence preview dibatasi.

Contoh:

- tampilkan 3 item awal;
- `+N bukti lainnya`;
- `Lihat semua` untuk detail.

Media:

- thumbnail/lazy load;
- full image/file hanya saat dibuka;
- jangan preload seluruh original media.

---

# 24. Reporting Model

## 24.1 Reportable = Exportable

Jika sebuah surface resmi disebut **laporan**, role berwenang harus dapat mengekspornya ke:

- PDF;
- XLSX/Excel.

Tidak semua halaman aplikasi harus mempunyai export.

## 24.2 Report UX

Report mengikuti pola:

```text
Summary
→ findings
→ detail dataset on demand
```

Jangan render ratusan row pada report home hanya untuk menunjukkan beberapa temuan.

## 24.3 Learning Record report

Teacher/TenantAdmin dapat memiliki `Laporan Perkembangan PKL` sesuai authorization.

Contoh filter:

- period;
- DUDI;
- assessment stage/status;
- monitoring status;
- search student;
- additional filters via `Filter lainnya` bila perlu.

Tidak ada AI insight.

---

# 25. Export Contract

## 25.1 Export form kecil

Sebelum export, tampilkan small configuration form dengan default yang sudah berguna.

Minimal:

- Format: PDF / Excel;
- Scope data;
- Jumlah record;
- Urutan.

Contoh:

```text
Export laporan

Format
● PDF
○ Excel

Data
● Sesuai filter saat ini
○ Halaman ini saja
○ Pilih jumlah

Jumlah
[100 ▼]

Urutan
[Terbaru dahulu ▼]

[Export]
```

## 25.2 Quantity choices

Rekomendasi preset:

- 25
- 50
- 100
- 250
- 500
- Semua

`Semua` harus tunduk pada safety limit/background export policy.

## 25.3 Pagination ≠ export scope

Browser dapat menampilkan 25 rows/page sementara export menghasilkan 100/500/all filtered records.

Pagination browsing dan export dataset tidak boleh dicampur.

## 25.4 PDF

PDF adalah human-readable report.

Default PDF sebaiknya tidak mencoba memasukkan ribuan rows.

Rekomendasi default:

- `Sesuai filter saat ini`;
- limit 100 records;
- jika terlalu besar, arahkan mempersempit filter atau pilih quantity.

PDF boleh merangkum summary + selected dataset.

## 25.5 XLSX

XLSX adalah data-oriented export dan dapat menangani dataset yang lebih besar.

XLSX tetap wajib:

- tenant scoped;
- filter aware;
- deterministic sorting;
- formula-injection safe;
- correct data types.

## 25.6 Large exports

Jika dataset melebihi synchronous safety threshold:

```text
request
→ background worker
→ generate file
→ notify user
→ download
```

Jangan membuat browser menunggu request sangat panjang.

## 25.7 Shared query contract

UI report, PDF, dan XLSX harus menggunakan semantic query/filter yang sama.

Secara konseptual:

```text
ReportQuery
- TenantScope
- Period
- Search
- Filters
- Sort
- Range/Limit
```

Dilarang membuat export yang diam-diam mengabaikan filter UI.

---

# 26. Learning Record Report Fields

Candidate fields, hanya bila domain-backed:

- Student;
- DUDI;
- Placement period;
- assessment stage status;
- criterion;
- Middle score;
- Final score;
- evaluator;
- monitoring status;
- evidence count;
- assessment completion status.

UI tidak harus menampilkan semua field sekaligus.

PDF tidak harus memiliki semua kolom XLSX.

---

# 27. API / Backend Behavioral Contract

Exact endpoint naming mengikuti arsitektur existing, tetapi backend harus mendukung semantic operations berikut.

## 27.1 Assessment template

- list template DUDI;
- create draft template;
- update allowed draft/version;
- activate/version template;
- retrieve current template;
- retrieve placement snapshot.

## 27.2 Assessment

- get stage state;
- create/load draft;
- update criterion result;
- select evidence;
- finalize;
- read finalized;
- TenantAdmin reopen with reason.

## 27.3 Learning Record

- student own record;
- teacher scoped record;
- mentor placement-scoped record;
- TenantAdmin tenant report query;
- criterion progression detail.

## 27.4 Teacher monitoring

- create monitoring event;
- list scoped monitoring timeline;
- visibility filtering.

## 27.5 Reporting/export

- paged report query;
- PDF generation;
- XLSX generation;
- background export for large datasets where threshold exceeded.

---

# 28. Suggested Data Model

Names below are conceptual, not mandatory exact EF entity names.

## 28.1 AssessmentTemplate

```text
AssessmentTemplate
- Id
- TenantId
- DudiId
- Version
- Status
- CreatedBy
- CreatedAt
- ActivatedAt?
```

## 28.2 AssessmentTemplateCriterion

```text
AssessmentTemplateCriterion
- Id
- TemplateId
- Name
- Description
- SortOrder
- IsActive
```

No Weight field required for V3 Learning Record.

## 28.3 PlacementAssessmentSnapshot

```text
PlacementAssessmentSnapshot
- Id
- TenantId
- PlacementId
- SourceTemplateId
- SourceTemplateVersion
- CreatedAt
```

## 28.4 PlacementAssessmentCriterionSnapshot

```text
PlacementAssessmentCriterionSnapshot
- Id
- SnapshotId
- Name
- Description
- SortOrder
```

## 28.5 Assessment

```text
Assessment
- Id
- TenantId
- PlacementId
- SnapshotId
- Stage            // Middle | Final
- Status           // Draft | Finalized | Reopened
- EvaluatorId
- OverallNote
- FinalizedAt?
- ReopenedAt?
```

Recommended uniqueness invariant:

```text
UNIQUE(PlacementId, Stage)
```

Evaluator provenance remains captured separately.

## 28.6 AssessmentCriterionResult

```text
AssessmentCriterionResult
- Id
- AssessmentId
- CriterionSnapshotId
- Score        // 1..5
- Comment?
```

## 28.7 AssessmentCriterionEvidence

```text
AssessmentCriterionEvidence
- AssessmentCriterionResultId
- JournalId / EvidenceId
- LinkedAt
```

## 28.8 TeacherMonitoringEvent

```text
TeacherMonitoringEvent
- Id
- TenantId
- PlacementId
- TeacherId
- Status
- Note?
- Visibility      // StudentVisible | Internal
- CreatedAt
```

## 28.9 AssessmentReopenAudit

May use existing audit infrastructure rather than a dedicated entity if existing audit guarantees actor/reason/state transition.

---

# 29. State Machines

## 29.1 Assessment

```text
NotDue
  ↓ due reached
Draft/Available
  ↓ finalize
Finalized
  ↓ TenantAdmin reopen(reason)
Reopened
  ↓ Mentor finalize
Finalized
```

`Overdue` is recommended as a computed operational state when due passed and no finalized result exists, not necessarily a persistent database status.

## 29.2 Template

```text
Draft
  ↓ activate
Active Version
  ↓ change requested
New Draft Version
  ↓ activate
New Active Version
```

Old used version remains historical.

---

# 30. Error Handling

## 30.1 Expected errors

Provide domain-safe user messages for:

- assessment not yet available;
- assessment already finalized;
- assessment reopened;
- missing required score;
- missing overall note;
- invalid evidence;
- evidence no longer accessible;
- unauthorized placement;
- stale write/version conflict;
- export too large for synchronous generation.

Never expose:

- SQL messages;
- stack traces;
- tenant internals;
- MinIO keys;
- authorization claims;
- raw exception details.

## 30.2 Optimistic concurrency

Where assessment/template edits can conflict, use existing optimistic concurrency/version pattern if available.

Avoid silent last-write-wins for finalized-sensitive data.

---

# 31. Backward Compatibility

V3 must preserve V2 data.

Existing assessments that do not contain Middle/Final progression metadata must remain readable.

Recommended legacy behavior:

- treat existing final assessment as legacy/final-only historical record;
- do not fabricate a Middle value;
- display `Penilaian Tengah tidak tersedia pada data lama` only where such explanation is needed internally;
- do not expose developer/API terminology.

Existing journal/evidence/portfolio/certificate/CV behavior must not regress.

---

# 32. Migration Strategy

If schema changes are required:

1. add V3 tables/fields non-destructively;
2. migrate existing relevant final assessment safely when mapping is deterministic;
3. do not fabricate Middle assessments;
4. preserve audit/history;
5. run migration from clean test DB;
6. run deterministic seeder;
7. run full E2E.

Migration must not require manual editing of production rows under normal deployment.

---

# 33. Reporting Performance Requirements

For potentially large queries:

- project only required columns;
- avoid N+1 queries;
- use database-side filters/sort/pagination;
- add indexes based on measured query path, especially Placement/Tenant/Stage/Status relationships;
- do not load evidence blobs/media when rendering list rows;
- total count query should be deliberate and optimized;
- background export streams/generates without holding entire dataset in browser memory.

No premature distributed caching requirement is introduced by V3.

---

# 34. Accessibility Requirements

V3 must preserve V2.1 accessibility principles:

- status never color-only;
- score represented as numeric/semantic text;
- table headers semantic;
- pagination keyboard accessible;
- assessment controls labelled;
- evidence picker keyboard usable;
- focus states visible;
- dialogs/sheets accessible;
- touch targets >=44px on mobile;
- comments/rubrics readable without hover dependency.

---

# 35. Responsive UX Requirements

Verify at:

- 320
- 360
- 390
- 414
- 768
- 1280+
- 1440x900

Requirements:

- no page-level horizontal overflow;
- assessment criterion form remains usable on mobile;
- evidence picker uses appropriate mobile sheet/full-page behavior;
- Teacher reporting rows stack on mobile;
- pagination remains reachable;
- export form becomes mobile sheet where appropriate;
- large tables use reduced/stacked rows instead of uncontrolled page overflow.

---

# 36. Acceptance Criteria — Assessment Template

V3 template feature passes when:

- Mentor within DUDI scope can create/edit allowed template;
- Mentor outside DUDI cannot;
- default Communication/Teamwork/Leadership can be present initially;
- DUDI can remove them;
- DUDI can add custom criterion;
- >20 criterion rejected;
- activated/used historical version does not mutate Placement snapshot;
- changes produce new version where required;
- template operations audited.

---

# 37. Acceptance Criteria — Middle Assessment

- Before midpoint: not due.
- At midpoint: available/perlu diisi.
- After midpoint without finalize: overdue.
- Mentor can score all snapshot criteria 1–5.
- Mentor sees rubric.
- criterion comment optional.
- overall note required.
- only Approved journal evidence selectable.
- evidence optional.
- finalize is idempotent.
- Student can see result after finalize.
- Teacher in scope can see result/evidence.
- finalized assessment locked.

---

# 38. Acceptance Criteria — Final Assessment

- available H-7 before EndDate;
- Mentor sees Middle results while filling Final;
- no auto-copy score;
- same criterion snapshot as Middle;
- Middle evidence may be reused;
- same validation as Middle;
- Student sees after finalize;
- Learning Record default switches to Final;
- Middle remains visible in history.

---

# 39. Acceptance Criteria — Reopen

- Mentor cannot reopen;
- Teacher cannot reopen;
- TenantAdmin same tenant can reopen;
- reason required;
- cross-tenant reopen denied;
- audit created;
- previous finalized result remains visible with `Sedang diperbaiki` state;
- draft changes not public/internal-current until re-finalized;
- re-finalization updates latest finalized result.

---

# 40. Acceptance Criteria — Learning Record

- menu/surface exists from beginning of PKL;
- before Middle: process state only;
- after Middle: Middle values shown;
- after Final: Final values are latest/default;
- detail shows Middle → Final;
- no percentage improvement;
- no automatic “membaik/memburuk” judgment;
- placement grouping preserved;
- latest placement first;
- company criterion names not merged automatically;
- evidence/comment detail only when opened;
- private/internal authorization enforced.

---

# 41. Acceptance Criteria — Teacher Monitoring

- Teacher can create optional monitoring event;
- Teacher chooses status manually;
- negative statuses require reason;
- note visibility StudentVisible/Internal honored;
- Student cannot read Internal notes;
- Mentor cannot read Teacher monitoring;
- monitoring history preserved;
- system never auto-flags based solely on score;
- assessment overdue appears as Teacher exception without write access to assessment.

---

# 42. Acceptance Criteria — Pagination / Large Data

- large dataset endpoint returns page, pageSize, totalCount/totalPages as required;
- browser only receives requested page items;
- search executes server-side;
- filter executes server-side;
- sort executes server-side;
- filter change resets page;
- report/detail URL preserves query state where applicable;
- mobile page has no horizontal overflow;
- small bounded datasets do not show pointless pagination.

---

# 43. Acceptance Criteria — Export

## PDF

- genuine PDF, not browser print masquerading as export;
- filter-aware;
- quantity-aware;
- readable report structure;
- default bounded quantity;
- valid non-empty file;
- authorization/tenant scope enforced.

## XLSX

- genuine XLSX;
- filter-aware;
- quantity-aware;
- correct row count;
- safe against formula injection;
- tenant scope enforced;
- supports larger dataset than default PDF.

## Large export

- threshold exceeding sync policy triggers background job;
- job tied to requesting authorized user/tenant;
- notification/download available after completion;
- expired/unauthorized file access denied.

---

# 44. Required Test Matrix

## 44.1 Unit/domain

Test at minimum:

- score 0 rejected;
- 1–5 accepted;
- 6 rejected;
- midpoint calculation;
- Final H-7 calculation;
- overdue state;
- template snapshot immutability;
- template version creation;
- maximum criterion limit;
- evidence Approved validation;
- evidence other placement rejected;
- evidence other tenant rejected;
- overall note required;
- finalize idempotency;
- reopen authorization;
- reopen reason required;
- Learning Record latest finalized selection;
- monitoring visibility;
- reminder deduplication.

## 44.2 Integration/backend

Test:

- Mentor DUDI scope;
- Teacher scope;
- TenantAdmin tenant scope;
- Student own record only;
- cross-tenant denial;
- replacement Mentor provenance;
- placement date change before unfinalized assessment;
- Middle overdue + Final available;
- report pagination/filter/sort;
- export respects filters.

## 44.3 E2E

Deterministic E2E should cover:

1. Mentor creates/uses DUDI template.
2. Placement snapshots template.
3. Middle becomes due.
4. Mentor scores + overall note + Approved evidence.
5. Finalize Middle.
6. Student sees Middle in Perkembangan.
7. Teacher sees progression/evidence.
8. Final becomes available H-7.
9. Mentor sees Middle while filling Final.
10. Mentor finalizes Final.
11. Student latest result becomes Final while Middle remains history.
12. TenantAdmin reopens Final with reason.
13. Student sees last finalized + correction state.
14. Mentor re-finalizes.
15. New finalized becomes current.
16. Report server pagination works.
17. PDF export obeys filter/limit.
18. XLSX obeys filter/limit and formula safety.

---

# 45. Performance Test Scenarios

At minimum synthetic data scenarios:

- 25 students;
- 315 students;
- 5,000 report rows;
- high evidence-count placement without loading full media in list;
- XLSX background export above synchronous threshold.

Success is not tied to one arbitrary millisecond target in PRD. Engineering should measure query count, payload size, memory behavior, and no full-dataset browser hydration.

---

# 46. Audit Events

Audit at minimum:

- template created;
- template version activated;
- assessment finalized;
- assessment reopened;
- assessment re-finalized;
- evidence association changes before finalize where audit policy requires;
- TenantAdmin sensitive actions;
- export request where existing audit policy treats it as relevant.

Do not audit every harmless read interaction as noise.

---

# 47. Recommended Delivery Slices

## V3 Slice 1 — Assessment Domain Foundation

- stage model Middle/Final;
- global score scale;
- DUDI template/versioning;
- Placement template snapshot;
- authorization;
- migration/tests.

**Complexity:** ~2–3/5.

## V3 Slice 2 — Mentor Assessment UX

- due-state;
- Middle form;
- Final form;
- Middle visible during Final;
- comments;
- overall note;
- Approved evidence picker;
- finalize lock;
- reminder.

**Complexity:** ~2.5–3/5.

## V3 Slice 3 — Learning Record

- Student `Perkembangan`;
- latest result;
- Middle → Final detail;
- evidence/comment details;
- multi-placement grouping;
- privacy/authorization.

**Complexity:** ~2/5.

## V3 Slice 4 — Teacher Monitoring Integration

- Teacher reads Learning Record;
- exception integration;
- monitoring event/status;
- visibility;
- timeline/follow-up;
- no Teacher scoring.

**Complexity:** ~2/5.

## V3 Slice 5 — Reporting + Large Data

- report summary/findings;
- server pagination;
- search/filter/sort;
- responsive rows;
- URL query state.

**Complexity:** ~2–2.5/5.

## V3 Slice 6 — Export

- small export configuration form;
- PDF;
- XLSX;
- quantity/scope controls;
- background large export;
- formula-injection protection;
- export authorization.

**Complexity:** ~2.5–3/5.

## V3 Slice 7 — Integrity & Regression

- reopen workflow;
- Mentor replacement edge cases;
- placement date changes;
- reminder idempotency;
- historical display snapshots;
- clean DB/migration/seeder gate;
- full E2E/performance/security regression.

**Complexity:** ~2–3/5.

---

# 48. Overall Complexity Assessment

| Domain | Complexity |
|---|---:|
| Template/versioning | 2–3/5 |
| Middle/Final lifecycle | 2/5 |
| Mentor assessment | 2–3/5 |
| Evidence linkage | 2–3/5 |
| Learning Record | 2/5 |
| Teacher monitoring integration | 2/5 |
| Authorization | 2–3/5 |
| Reporting pagination | 2/5 |
| PDF/XLSX export | 2–3/5 |
| Large background export | 3/5 |
| Edge-case integrity | 2–3/5 |
| **Overall V3** | **~2.5/5 (moderate, testable)** |

V3 sengaja menghindari subsystem yang akan menaikkan complexity ke 4–5/5 seperti competency ontology, automatic mapping, AI scoring, public selective Learning Record, atau configurable arbitrary assessment cadence.

---

# 49. Definition of Done

V3 dianggap selesai bila:

1. Mentor dapat menilai satu Placement pada Tengah dan Akhir dengan snapshot criterion yang sama.
2. Score selalu 1–5 dengan label global.
3. DUDI dapat mengelola criterion relevan tanpa taxonomy global.
4. Middle dan Final finalized terkunci.
5. TenantAdmin dapat reopen dengan alasan + audit.
6. Evidence opsional dapat dihubungkan per criterion hanya dari Approved journal.
7. Student melihat Perkembangan yang akurat dan private.
8. Teacher dapat membaca perkembangan dan melakukan monitoring tanpa memberi score.
9. Reminder Mentor berjalan idempotent.
10. Histori tetap benar jika Mentor/template/DUDI berubah.
11. Large reports menggunakan server-side pagination/filter/sort.
12. Genuine report dapat diekspor PDF/XLSX dengan configurable scope/quantity.
13. Large export tidak membebani browser.
14. Tenant isolation dan authorization memiliki automated test coverage.
15. Clean database → migration → seed → application → E2E berhasil.
16. V2 flows tidak regress.

---

# 50. Deferred Decisions / Future Versions

Secara eksplisit ditunda:

- timezone subsystem yang lebih kompleks;
- canonical competency mapping;
- mapping skill lintas DUDI;
- public Learning Record;
- selective recruiter sharing;
- AI interpretation;
- employment readiness;
- job marketplace;
- CV gating/application workflow;
- student-created non-PKL experience;
- competency normalization lintas perusahaan;
- arbitrary assessment frequency;
- portfolio/CV customization.

V3 harus selesai tanpa ketergantungan pada fitur-fitur tersebut.

---

# 51. Final Product Contract

Vokasia V3 harus dapat dijelaskan sederhana sebagai:

> **Vokasia merekam bagaimana siswa berkembang selama PKL berdasarkan penilaian nyata Mentor Industri pada pertengahan dan akhir PKL. Setiap hasil tetap terhubung ke perusahaan, evaluator, kriteria, komentar, dan bukti pekerjaan yang relevan. Guru menggunakan data tersebut untuk memonitor dan menindaklanjuti perkembangan siswa, bukan untuk menggantikan Mentor sebagai evaluator pekerjaan.**

Dan untuk skala data:

> **Vokasia menampilkan data secukupnya untuk manusia, mengambil data besar secara bertahap dari server, dan menyediakan PDF/XLSX yang dapat dikonfigurasi ketika sebuah dataset memang layak menjadi laporan.**

