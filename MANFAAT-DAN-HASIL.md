# MANFAAT & HASIL — VOKASIA

**Disusun:** 1 Agustus 2026 · angka diverifikasi langsung dari repo

---

## 0. PISAHKAN DULU: "HASIL" ITU DUA HAL BERBEDA

Kesalahan paling umum di bagian ini — dan yang paling gampang dijatuhkan penguji —
adalah mencampur dua hal yang bunyinya mirip:

| Istilah | Artinya | Kamu punya? |
|---|---|---|
| **Output** — hasil pembangunan | Apa yang berhasil dibuat | ✅ **Ada, bisa dihitung** |
| **Outcome** — hasil pemakaian | Apa yang berubah bagi pengguna | ❌ **Belum ada. Pilot belum jalan** |
| **Impact** — dampak jangka panjang | Apakah lulusan lebih mudah kerja | ❌ Butuh tahunan |

Hal yang sama berlaku untuk "manfaat". Manfaat yang belum diuji bukan temuan — dia
**klaim**. Boleh disebut, asal dilabeli jujur.

Karena itu setiap manfaat di dokumen ini diberi label:

- **[TURUNAN]** — mekanismenya ada di kode, bisa kamu tunjukkan sekarang juga
- **[ASUMSI]** — masuk akal, target sudah ditetapkan, tapi belum diukur
- **[HIPOTESIS]** — bisa saja salah; hanya pilot yang bisa menjawab

Kalimat yang menyelamatkanmu dari pertanyaan menjebak:

> "Manfaat yang saya sebut ini turunan dari mekanisme yang sudah jalan. Yang belum saya
> buktikan adalah apakah orang benar-benar memakainya."

---

# BAGIAN A — HASIL (OUTPUT)

Ini yang sudah nyata. Semua angka dihitung dari repo, bisa diverifikasi ulang di depan
penonton dengan satu perintah terminal.

## A1. Perangkat lunak yang jadi

| Metrik | Angka |
|---|---|
| Baris kode backend (C#) | ~22.500 |
| Baris kode frontend (TS/TSX) | ~12.300 |
| Halaman/route frontend | 26 |
| Grup endpoint API | 19 file |
| Consumer antrian pesan | 13 |
| Cron job terjadwal | 6 |
| Migration database | 6 |
| Entity domain | 15 file |
| Container produksi | 7 |

## A2. Bukti kualitas

| Metrik | Angka |
|---|---|
| File test | 86 |
| Method test (`[Fact]`/`[Theory]`) | 304 (3 di-skip, alasan terdokumentasi) |
| Kasus `[InlineData]` tambahan | 37 |
| Integration test | Terhadap **Postgres & RabbitMQ asli** via Testcontainers, bukan mock |
| Laporan keamanan | `REPORT-SECURITY.md` — NFR-SEC-01 s/d 08 dipetakan ke file & baris |

> Yang layak ditekankan bukan angka 304, tapi **Testcontainers**. Banyak proyek mengklaim
> "sudah ditest" padahal seluruh test-nya berjalan di atas database palsu. Test-mu jalan
> di atas Postgres dan RabbitMQ sungguhan di dalam container.

## A3. Cakupan fungsional

Siklus PKL penuh, dari awal sampai akhir:

```
Periode → Penempatan → Jurnal harian → Approval mentor
  → Kunjungan guru → Penilaian rubrik dua sisi → Sertifikat ber-QR → Portofolio publik
```

Ditambah: 8 role dengan RBAC dua lapis · multi-tenant · magic link mentor · deteksi
ghosting · billing + invoice · impersonation teraudit · audit log · portofolio opt-in.

## A4. Hasil proses (ini yang sering dilupakan)

Untuk presentasi akademis, cara kerjanya sendiri adalah hasil:

| Metrik | Angka |
|---|---|
| Tiket sprint diselesaikan | 21 (H1 s/d H7) |
| Keputusan terdokumentasi | `DECISIONS.md`, bernomor kronologis |
| Skill AI terkunci hash | 44 (`skills-lock.json`) — setup reproducible |
| MCP server terpakai | 7 |
| Model kerja | 1 manusia + 4 peran AI dengan wilayah terkunci |

## A5. Hasil ekonomi (model, belum tervalidasi pasar)

| Item | Angka |
|---|---|
| Biaya operasional | Rp 1,5–1,7 jt/bulan |
| Paket Starter | Rp 499.000/bln · 200 siswa |
| Paket Professional | Rp 1.499.000/bln · 1.000 siswa |
| Paket Enterprise | Rp 3.999.000/bln · 5.000 siswa |
| **Titik impas** | **1 sekolah Professional, atau 3–4 Starter** |

**[HIPOTESIS]** — harga ini belum pernah diuji ke sekolah sungguhan. Nol pelanggan
membayar. Sebut angkanya, tapi sebut juga statusnya.

---

# BAGIAN B — MANFAAT PER PIHAK

Manfaat harus disebut **per aktor**, bukan sebagai daftar fitur. Sekolah tidak membeli
fitur; mereka membeli hilangnya satu kerepotan tertentu.

## B1. Siswa

| Manfaat | Mekanismenya | Label |
|---|---|---|
| Punya bukti kompetensi yang bisa diverifikasi orang lain | Sertifikat PDF ber-QR + endpoint `/verify/{code}` publik | **[TURUNAN]** |
| Portofolio yang bisa dilampirkan saat melamar | `/p/{slug}`, opt-in, tanpa NISN/kontak | **[TURUNAN]** |
| Mengisi jurnal jauh lebih cepat dari menulis buku | Target ≤2 menit, payload `/student` <200KB | **[ASUMSI]** — target ditetapkan, belum diukur |
| Tahu posisinya sendiri (streak, status) tanpa menunggu guru | `StudentDailyStatus` + streak counter | **[TURUNAN]** |
| Portofolio benar-benar membantu diterima kerja | — | **[HIPOTESIS]** — belum ada bukti HRD mau melihatnya |

## B2. Guru pembimbing

| Manfaat | Mekanismenya | Label |
|---|---|---|
| Tahu siswa berhenti mengisi dalam **1 hari**, bukan berminggu-minggu | Cron `FlagGhostingStudents` 21:00 WIB → status merah → notifikasi | **[TURUNAN]** |
| Tidak perlu datang ke lokasi hanya untuk tahu status | Dashboard RAG per siswa | **[TURUNAN]** |
| Catatan kunjungan tersimpan di sistem, bukan di buku pribadi | `VisitEndpoints` — catatan, foto, tanda tangan | **[TURUNAN]** |
| Beban administrasinya benar-benar turun | — | **[HIPOTESIS]** — bisa saja justru bertambah di awal |

> **Ini manfaat paling kuat di seluruh daftar.** Perpindahan waktu deteksi dari *minggu*
> ke *hari* adalah satu-satunya klaim yang mekanismenya bisa kamu tunjuk di layar dan
> logikanya tidak bisa dibantah. Jadikan ini poin utama.

## B3. Waka Hubin / admin sekolah

| Manfaat | Mekanismenya | Label |
|---|---|---|
| Melihat status 300 siswa sekaligus, bukan satu per satu | Dashboard RAG hijau/kuning/merah | **[TURUNAN]** |
| Rekap nilai tidak diketik ulang ke Excel | `GradeRecapEndpoints` + export async | **[TURUNAN]** |
| Punya dokumentasi kepatuhan kurikulum untuk dinas | Jurnal + penilaian + sertifikat tersimpan & tidak bisa diubah | **[TURUNAN]** |
| Data PKL tidak hilang saat guru pindah/pensiun | Data di sistem, bukan di buku perorangan | **[TURUNAN]** |
| Impor siswa tidak diketik manual | Impor CSV kolom Dapodik, error per baris | **[TURUNAN]** |

## B4. Mentor industri (DUDI)

| Manfaat | Mekanismenya | Label |
|---|---|---|
| Tidak perlu membuat akun atau mengingat password | Magic link, grant kustom, token 72 jam sekali pakai | **[TURUNAN]** |
| Approve sepuluh jurnal sekaligus, sekali seminggu | Batch approve + digest mingguan Senin 06:00 | **[TURUNAN]** |
| Tidak terikat satu sekolah | Mentor lintas-tenant, difilter per penempatan | **[TURUNAN]** |
| **Mentor benar-benar mau memakainya** | — | **[HIPOTESIS]** ← **risiko terbesar produk ini** |

> Baris terakhir jangan disembunyikan. Justru sebutkan: *"Seluruh alur ini berhenti
> kalau mentor tidak mau pakai. Itu asumsi paling rapuh di produk saya, dan itu yang
> paling ingin saya uji duluan."* Jawaban seperti ini membuat orang percaya pada
> klaim-klaimmu yang lain.

## B5. Sekolah sebagai institusi

- Kepatuhan Kepmendikbudristek 262/M/2022 terdokumentasi otomatis — **[TURUNAN]**
- Data anak dilindungi sesuai UU PDP: EXIF-GPS dihapus, portofolio opt-in tanpa NISN,
  retensi foto 2 tahun, hak hapus saat lulus — **[TURUNAN]**
- Biaya lebih murah daripada membangun sistem sendiri — **[ASUMSI]**

## B6. Manfaat bagi pengembang (untuk presentasi akademis)

Kalau formatnya menuntut "manfaat bagi penulis", ini yang jujur:

- Menerapkan pola arsitektur yang jarang dipakai di proyek skala tugas: transactional
  outbox, BFF, multi-tenant ORM filter, idempotent consumer, DLQ
- Membangun disiplin verifikasi: test wajib dibuktikan bisa merah dulu, klaim "sudah
  ditest" ditolak tanpa output eksekusi
- Menguji model kerja solo + AI berperan dengan wilayah terkunci — dan menemukan
  batasnya sendiri

---

# BAGIAN C — YANG BELUM TERBUKTI (JANGAN DILEWATI)

Bagian ini yang membedakan presentasi jujur dari brosur. Sediakan satu slide khusus.

| Klaim | Kenapa belum terbukti |
|---|---|
| Siswa konsisten mengisi 6 bulan | Nol siswa nyata. Target 75% belum diuji |
| Mentor bertahan sampai akhir periode | Nol mentor nyata. Magic link masih hipotesis |
| Guru benar-benar terbantu | Bisa saja bertambah beban di masa transisi |
| Sekolah mau membayar | Nol pelanggan |
| Portofolio dilihat HRD | Belum ada satu pun HRD yang membukanya |
| Sistem tahan beban 50 req/dtk | Target NFR, belum ada uji beban nyata |
| Uptime 99% | Belum ada produksi berjalan |

**Cara mengucapkannya:**

> "Yang sudah selesai adalah sistemnya. Yang belum terbukti adalah apakah manusia mau
> memakainya. Keduanya pekerjaan berbeda, dan saya baru menyelesaikan yang pertama."

---

# BAGIAN D — CARA MENGUKUR MANFAAT (supaya bukan sekadar klaim)

Manfaat tanpa alat ukur cuma iklan. Ini pasangan ukurannya:

| Manfaat yang diklaim | Cara mengukur | Baseline yang wajib dicatat **sebelum** |
|---|---|---|
| Deteksi lebih cepat | Selisih hari: siswa berhenti mengisi → guru tahu | Tanya guru: "sekarang berapa lama?" |
| Pengisian jurnal naik | % hari terisi dari total hari kerja | Hitung 30 buku jurnal kertas |
| Isi jurnal lebih cepat | Stopwatch, 10 siswa | Ukur waktu menulis buku manual |
| Rekap lebih cepat | Stopwatch, satu putaran penuh | Ukur proses Excel manual |
| Mentor bertahan | % mentor masih approve di bulan ke-3 | % formulir kertas yang kembali |
| Beban admin turun | Wawancara: "apa yang sekarang tidak perlu Anda lakukan?" | Catat daftar tugas saat ini |

**Peringatan yang paling sering diabaikan:** baseline hanya bisa diambil **sebelum**
sistem dipasang. Sekali PKL berjalan, kesempatan itu hilang dan tidak bisa diulang.
Tanpa baseline, tidak ada kontras. Tanpa kontras, tidak ada manfaat yang bisa dibuktikan
— cuma perasaan.

---

# BAGIAN E — KAITAN SDG / TPB

Kalau presentasimu menuntut bagian ini, jangan menempel logo. Cantumkan target, indikator,
dan status kejujurannya.

| Tujuan | Target spesifik | Indikator di Vokasia | Status |
|---|---|---|---|
| **SDG 4** — Pendidikan Bermutu | 4.4: menambah pemuda dengan keterampilan relevan untuk kerja | Jumlah sertifikat terverifikasi terbit; % kompetensi tercatat per siswa | **Mekanisme ada, angka nol** |
| **SDG 8** — Pekerjaan Layak | 8.6: mengurangi pemuda tidak bekerja/sekolah/berlatih | Tingkat penyerapan kerja lulusan pemegang portofolio | **Belum bisa diukur** — butuh pelacakan alumni bertahun |

Mekanisme dampaknya, dinyatakan terus terang:

> PKL 6 bulan saat ini tidak meninggalkan bukti apa pun. Vokasia mengubahnya jadi
> artefak yang bisa diverifikasi. **Hipotesisnya**: bukti yang bisa diverifikasi
> menaikkan peluang lulusan diterima kerja.
>
> Hipotesis itu **belum diuji**. Rantainya panjang — bukti ada ≠ HRD melihatnya ≠ HRD
> mempertimbangkannya ≠ lulusan diterima. Setiap mata rantai bisa putus.

Menyatakan rantai sebab yang bisa putus, alih-alih mengklaim dampak langsung, adalah
yang membedakan analisis dari promosi.

---

# BAGIAN F — SLIDE RINGKAS

> **HASIL**
> Sistem manajemen PKL multi-tenant, siklus penuh, siap deploy.
> 34.800 baris kode · 304 test terhadap Postgres & RabbitMQ asli · 7 container ·
> 21 tiket · 26 halaman · 13 consumer · 6 cron.
> Dibangun solo dengan AI berperan, 44 skill terkunci hash.
>
> **MANFAAT UTAMA** — satu per pihak, ambil yang terkuat saja:
> · Guru — tahu siswa bermasalah dalam **1 hari**, bukan berminggu-minggu
> · Waka Hubin — status 300 siswa dalam satu layar; rekap nilai tanpa ketik ulang
> · Mentor — tanpa akun, tanpa password, approve mingguan sekali klik
> · Siswa — 6 bulan PKL berubah jadi **bukti yang bisa diverifikasi siapa pun**
>
> **YANG BELUM TERBUKTI**
> Apakah manusia mau memakainya. Nol pilot, nol pelanggan.
> Itu pekerjaan berikutnya.

---

## SATU HAL YANG PERLU KAMU INGAT

Presentasi teknis dinilai dari **kalibrasi**, bukan dari besarnya klaim.

Kalau setiap baris di bagian manfaatmu terdengar berhasil, penguji akan mencari yang
kamu sembunyikan — dan mereka akan menemukannya. Kalau kamu sendiri yang menunjukkan
batasnya, sisa presentasimu jadi bisa dipercaya.
