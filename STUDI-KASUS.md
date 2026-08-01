# STUDI KASUS VOKASIA — Rancangan & Peringatan

**Diverifikasi dari `DemoSeeder.cs`:** 1 Agustus 2026

---

## 0. DUDUK PERKARA DULU

"Studi kasus" bisa berarti tiga hal yang sangat berbeda. Kamu harus tahu kamu sedang
diminta yang mana, karena bahannya beda total:

| Jenis | Isinya | Kamu punya? |
|---|---|---|
| **A. Skenario ilustratif** — cerita satu siswa untuk membawakan demo | Data seed, ditandai jelas sebagai contoh | ✅ Ada (tapi bermasalah, lihat §2) |
| **B. Studi kasus nyata** — sekolah sungguhan memakai produk, dengan hasil terukur | Data pilot: sebelum vs sesudah | ❌ **Belum ada. Pilot belum jalan.** |
| **C. Studi kasus sebagai metode riset** — analisis mendalam satu sekolah untuk memahami masalah | Wawancara, observasi, dokumen | ❌ Tidak ada bukti di repo |

**Aturan yang tidak boleh dilanggar:**
Jangan pernah menyajikan A seolah-olah B. Begitu kamu bilang "SMK Negeri 1 Jakarta
memakai Vokasia dan tingkat pengisian jurnal naik jadi 95%", dan itu berasal dari
`faker.Random`, kamu sedang mengarang data. Satu orang yang mengeceknya dan seluruh
presentasimu selesai.

Kalimat aman yang harus kamu ucapkan sebelum demo:

> "Data yang kalian lihat ini data contoh, bukan sekolah sungguhan. Belum ada pilot.
> Yang saya tunjukkan adalah **alurnya**, bukan hasilnya."

Kalimat itu tidak melemahkan presentasimu. Justru sebaliknya — orang teknis dan orang
sekolah sama-sama langsung tahu kamu bisa dipercaya.

---

## 1. YANG SEBENARNYA ADA DI DATA SEED

Dibaca langsung dari `DemoSeeder.cs`:

| Parameter | Nilai default |
|---|---|
| Tenant (sekolah) | 3 |
| Siswa per tenant | 30 → **90 penempatan** |
| DUDI | 10 |
| Rentang hari | 60 hari (≈43 hari kerja) |
| Peluang jurnal terisi per hari | 95% |
| Estimasi total entri jurnal | ≈ 3.600 |

**Tiga sekolah demo:**

| Sekolah | NPSN | Kota | Paket |
|---|---|---|---|
| SMK Negeri 1 Jakarta | 20101001 | Jakarta Pusat | Professional |
| SMK Negeri 2 Bandung | 20202002 | Bandung | Professional |
| SMK Negeri 5 Surabaya | 20303003 | Surabaya | Starter |

**Harga paket (ini menjawab pertanyaan "berapa harganya" yang pasti muncul):**

| Paket | Harga/bulan | Kuota siswa |
|---|---|---|
| Starter SMK | Rp 499.000 | 200 |
| Professional SMK | Rp 1.499.000 | 1.000 |
| Enterprise Multi-Campuses | Rp 3.999.000 | 5.000 |

Cocokkan dengan biaya operasional Rp 1,5–1,7 jt/bln → **break-even di sekitar 3–4
sekolah paket Starter, atau 1 sekolah Professional.** Angka ini kuat, pakai.

---

## 2. ⚠️ BLOKER — DEMO-MU KEMUNGKINAN BESAR TIDAK PUNYA SISWA MERAH

Ini temuan paling penting di dokumen ini. Perbaiki sebelum presentasi.

### Masalahnya

Seeder menentukan jurnal terisi atau tidak secara acak:

```csharp
if (faker.Random.Double() > 0.05)   // 95% terisi, 5% kosong — independen tiap hari
```

Status MERAH butuh **3 hari kerja kosong berturut-turut di akhir periode**.

Hitungannya:

| Peristiwa | Peluang |
|---|---|
| Satu siswa berakhir MERAH | 0,05³ = **0,0125%** |
| Dari 90 siswa, **tidak ada satu pun** yang MERAH | **98,9%** |
| Dari 90 siswa, ada minimal satu MERAH | **1,1%** |

**Artinya: 99 dari 100 kali kamu menjalankan seed, tidak ada siswa berstatus MERAH
sama sekali.**

### Kenapa ini fatal untuk presentasi

Deteksi ghosting adalah **fitur yang paling laku ke sekolah**. Itu tahap 7 di naskah
presentasi — momen di mana Waka Hubin sadar dia bisa tahu siswa bermasalah tanpa
menelepon satu per satu.

Kalau dashboard-mu hijau semua, fitur itu tidak bisa kamu tunjukkan. Kamu cuma bisa
menceritakannya. Bedanya besar.

### Masalah turunan

- **Nol jurnal berstatus `Rejected`.** Seeder menulis semuanya `Approved`. Padahal
  alur "mentor menolak jurnal + kasih catatan" adalah bagian dari cerita.
- **PRD FR-X-04 tidak terpenuhi.** Bunyinya: *"Seeder demo: 3 tenant, 100 DUDI, 900
  siswa, 90 hari jurnal (termasuk skenario ghosting & rejected)."* Skenario ghosting
  dan rejected **tidak dijamin ada** — cuma kebetulan statistik. Dan volumenya jauh di
  bawah spesifikasi (10 DUDI vs 100, 90 siswa vs 900, 60 hari vs 90).
- **Teks jurnal identik semua.** Setiap entri berbunyi sama persis: *"Mengikuti kegiatan
  pengerjaan tugas harian di ... Melakukan pemeliharaan jaringan dan dokumentasi."*
  Begitu kamu buka daftar jurnal di depan penonton, keseragamannya langsung kelihatan
  palsu.

### Perbaikan minimum sebelum presentasi

Jangan rombak seeder. Cukup **paksa beberapa siswa masuk skenario tertentu**, sisanya
biarkan acak:

| Siswa ke- | Skenario yang dipaksa | Untuk mendemokan |
|---|---|---|
| #1 | Isi rajin, streak panjang, semua approved | Kondisi normal + streak |
| #2 | 3 hari kerja terakhir kosong → **MERAH** | Deteksi ghosting (tahap 7) |
| #3 | 1 hari terakhir kosong → **KUNING** | Peringatan dini |
| #4 | Ada 2 jurnal `Rejected` + catatan mentor | Alur revisi |
| #5 | Sudah dinilai + sertifikat terbit | Verifikasi QR (tahap 13–14) |

Sisanya (25 per sekolah) biarkan acak seperti sekarang — supaya dashboard tetap terlihat
alami, tidak seperti data yang disusun rapi.

Variasikan juga teks jurnal: siapkan 15–20 kalimat berbeda per jurusan, pilih acak.

---

## 3. ⚠️ EMAIL DAN NAMA ASLI DI DATA SEED

Seeder memakai **alamat email pribadi yang tampaknya nyata**:

- `mastergemerz2008@gmail.com` (admin SMKN 1 Jakarta)
- `masteralvano@gmail.com` (guru)
- `ivanowisnubudilaras2008@gmail.com` (siswa #1)
- `guru.dewi@gmail.com`, `guru.fajar@gmail.com`

Dan **nama tokoh publik nyata** sebagai siswa: *Bayu Skak*, *Cinta Laura*,
*Vina Panduwinata*.

Dua masalah:

1. **Risiko saat berbagi layar.** Kamu akan membuka daftar siswa di depan penonton.
   Alamat email pribadi orang akan terlihat, dan mungkin terekam kalau sesi direkam.
2. **Melanggar kontrak desainmu sendiri.** `DESIGN.md` menyatakan eksplisit:
   *"Placeholder data uji eksplisit palsu ('Siswa Contoh', bukan nama asli siapa pun)."*

Ganti sebelum presentasi. Pola aman: `siswa01@contoh.sch.id`, `admin@smkcontoh.sch.id`,
dan nama generik ("Siswa Contoh 01") atau nama umum tanpa asosiasi publik.

Ini juga bahan cerita yang bagus, kalau kamu berani: *"Produk ini soal data anak di
bawah umur. Waktu saya audit data demo saya sendiri, saya menemukan email pribadi nyata
di sana. Saya ganti. Kalau saya tidak disiplin di data palsu, saya tidak akan disiplin
di data asli."*

---

## 4. STRUKTUR STUDI KASUS UNTUK PRESENTASI (Jenis A)

Ini yang kamu bawakan besok. Bentuknya **kontras sebelum–sesudah**, bukan daftar fitur.

### Bingkai: satu sekolah, satu siswa, satu semester

> **Konteks.** SMK Negeri 1 Jakarta. 300 siswa kelas XII wajib PKL 6 bulan.
> Waka Hubin satu orang. Guru pembimbing 12 orang. Mitra DUDI 40 perusahaan.
> *(sekolah contoh — bukan data sekolah sungguhan)*

### Bagian 1 — Cara lama, dan di mana ia patah

Jangan bilang "sistem lama tidak efisien". Tunjukkan **titik patahnya**, satu per satu:

| Kegiatan | Cara lama | Yang patah |
|---|---|---|
| Jurnal harian | Buku tulis, diperiksa saat kunjungan | Guru baru tahu siswa berhenti mengisi **berminggu-minggu kemudian** |
| Pemantauan | Grup WhatsApp per kelas | Pesan penting tenggelam. Tidak ada riwayat yang bisa ditelusuri |
| Siswa menghilang | Ketahuan saat guru kunjungan | Bisa 3–4 minggu tanpa terdeteksi |
| Penilaian mentor | Formulir kertas, dititipkan ke siswa | Hilang, terlambat, kadang diisi asal |
| Rekap nilai | Ketik ulang ke Excel | Berhari-hari, rawan salah ketik |
| Bukti untuk siswa | Surat keterangan, kadang tidak ada | **Tidak bisa diverifikasi siapa pun** |

Baris terakhir adalah inti argumenmu. Yang lain adalah repot. Yang terakhir adalah
kerugian permanen bagi siswa.

### Bagian 2 — Titik ungkit, bukan daftar fitur

Sebut **tiga saja**. Lebih dari itu penonton lupa.

**(1) Dari "ketahuan belakangan" jadi "ketahuan besok pagi."**
Tiga hari kerja kosong → status merah otomatis jam 9 malam → guru dan admin tahu.
Bukan fitur baru — memindahkan waktu ketahuan dari minggu ke hari.

**(2) Dari "mentor harus dilatih" jadi "mentor klik link."**
Mentor industri tidak digaji sekolah. Setiap beban tambahan menaikkan peluang dia
berhenti pakai. Maka: tidak ada password, tidak ada pendaftaran. Klik link di email,
approve sepuluh jurnal sekaligus, seminggu sekali.

**(3) Dari "6 bulan hilang" jadi "6 bulan bisa diverifikasi."**
Sertifikat ber-QR yang bisa dicek HRD mana pun, plus portofolio publik opsional.

### Bagian 3 — Alur satu siswa

Pakai tabel 15 tahap di `PRESENTASI.md` §2. Jangan diulang di sini.

### Bagian 4 — Apa yang belum terbukti (**jangan dilewati**)

Bagian ini yang membedakan presentasi jujur dari brosur. Ucapkan apa adanya:

> "Yang belum saya buktikan ada tiga.
>
> Satu, apakah mentor industri benar-benar mau memakainya. Magic link itu **hipotesis**
> saya, bukan temuan.
>
> Dua, apakah siswa konsisten mengisi 6 bulan penuh. Target saya 75%. Saya belum tahu.
>
> Tiga, apakah sekolah mau membayar. Belum ada satu pun yang membayar.
>
> Ketiganya cuma bisa dijawab lewat pilot. Itu langkah saya berikutnya."

Ini bukan kelemahan presentasi. Ini yang membuat penonton percaya pada bagian lain.

---

## 5. RANCANGAN PILOT — CARA MENDAPAT STUDI KASUS NYATA (Jenis B)

Kalau kamu mau punya studi kasus sungguhan dalam 3 bulan, ini rancangannya.

### Pemilihan sekolah — 3 sekolah yang berbeda secara struktural

| Profil | Alasan | Yang diuji |
|---|---|---|
| SMK negeri besar (>500 siswa PKL) | Beban administrasi paling berat | Apakah skala jadi masalah? |
| SMK swasta kecil (<100 siswa) | Sumber daya IT minim | Apakah bisa jalan tanpa staf IT? |
| SMK luar Jawa | Infrastruktur internet lebih lemah | Apakah asumsi 3G/HP murah benar? |

Tiga sekolah yang mirip tidak menguji apa pun. Perbedaan itulah datanya.

### Ukuran yang harus dicatat — dan baselinenya

Kesalahan pilot paling umum: lupa mencatat kondisi **sebelum**. Tanpa baseline, kamu
tidak punya kontras, dan tanpa kontras tidak ada studi kasus.

| Ukuran | Baseline (catat SEBELUM mulai) | Target |
|---|---|---|
| Tingkat pengisian jurnal | Ambil sampel 30 buku jurnal kertas, hitung hari terisi | ≥75% |
| Waktu deteksi siswa bermasalah | Tanya guru: "berapa lama sampai tahu?" | ≤1 hari |
| Tingkat approval mentor | Berapa % formulir kertas kembali? | ≥70% mingguan |
| Waktu rekap nilai | Stopwatch, sekali putaran manual | <1 jam |
| Waktu isi jurnal | Ukur langsung 10 siswa pakai stopwatch | ≤2 menit |
| Retensi mentor | — | ≥60% masih aktif di bulan ke-3 |

**Catat baseline sebelum sistem dipasang.** Sekali PKL berjalan, kesempatan itu hilang
dan tidak bisa diulang.

### Yang tidak akan tertangkap angka

Wawancarai di akhir bulan pertama dan ketiga:

- **Waka Hubin** — apa yang sekarang tidak perlu dia lakukan lagi?
- **2 guru** — kapan terakhir dashboard mengubah tindakan mereka?
- **3 mentor** — berapa lama approve mingguan? Apa yang bikin malas?
- **5 siswa** — kapan mereka *tidak* mengisi, dan kenapa?
- **1 siswa yang berhenti mengisi** — ini yang paling berharga, dan paling sering
  tidak ditanya orang.

### Yang harus kamu siapkan mental untuk menerima

Pilot bukan alat pembuktian. Pilot adalah alat **pembatalan**. Tulis sekarang, sebelum
mulai, kondisi yang akan membuatmu mengakui asumsimu salah:

- Kalau pengisian jurnal **<50%** → produknya tidak menyelesaikan masalah siswa,
  cuma memindahkan kertas ke layar.
- Kalau retensi mentor **<40% di bulan ke-3** → magic link tidak cukup. Hambatannya
  bukan password, tapi motivasi.
- Kalau nol sekolah bersedia membayar setelah merasakan → masalahnya nyata, tapi tidak
  cukup menyakitkan untuk dibayar. Itu temuan penting, bukan kegagalan.

Menuliskan kondisi pembatalan **sebelum** pilot adalah yang membedakan pilot dari
pencarian pembenaran.

---

## 6. YANG HARUS DIKERJAKAN, BERURUTAN

**Sebelum presentasi:**

1. Perbaiki seeder — paksa 5 skenario (§2). Tanpa ini demo ghosting-mu tidak ada.
2. Ganti email pribadi dan nama tokoh publik (§3).
3. Variasikan teks jurnal, 15–20 kalimat per jurusan.
4. Jalankan seed, **buka dashboard, pastikan matamu melihat siswa merah dan kuning.**
5. Hafalkan kalimat pembuka: *"ini data contoh, bukan sekolah sungguhan."*

**Setelah presentasi, kalau mau studi kasus nyata:**

6. Pilih 3 sekolah yang berbeda strukturnya.
7. **Catat baseline dulu** — sebelum apa pun dipasang.
8. Tulis kondisi pembatalan, dan simpan di tempat yang akan kamu baca lagi.

---

## 7. SATU KALIMAT YANG PERLU KAMU INGAT

Studi kasus yang bagus bukan cerita tentang produk yang berhasil. Ia cerita tentang
**satu masalah yang jelas, dan apa yang berubah setelah disentuh** — termasuk yang
tidak berubah.

Kalau semua di studi kasusmu berhasil, tidak ada yang percaya.
