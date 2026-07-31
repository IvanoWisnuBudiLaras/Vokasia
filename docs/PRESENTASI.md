# PRESENTASI VOKASIA — Naskah & Struktur

**Durasi target:** 20 menit (bisa dipadatkan ke 10, lihat §7)
**Disusun:** 31 Juli 2026 · sumber: PRD.md, DESIGN.md, TICKETS.md, kode repo

---

## 0. KOREKSI ALUR YANG KAMU USULKAN

Urutan yang kamu ajukan:

1. Jelasin apps-nya kayak gimana
2. Terus teknologinya
3. Caramu bikin

**Masalahnya:** urutan ini benar isinya, salah di titik masuknya.

Kalau babak 1 dibuka dengan "ini aplikasi saya, ada menu ini itu", penonton belum punya
alasan untuk peduli. Tur menu ≠ demo. Mereka akan lihat 20 layar dan ingat nol.

**Perbaikannya cuma satu:** sisipkan **30 detik masalah** sebelum layar pertama, lalu
demo **satu alur utuh satu orang**, bukan tur fitur.

Urutan final yang dipakai naskah ini:

| Babak | Isi | Menit |
|---|---|---|
| 1 | Kenapa ini ada (masalah) | 2 |
| 2 | Alur — satu siswa, awal sampai akhir | 8 |
| 3 | Teknologi (dibenarkan oleh Babak 2) | 5 |
| 4 | Cara bikin | 4 |
| 5 | Tanya jawab | — |

---

## 1. BABAK 1 — KENAPA INI ADA (2 menit)

> Jangan buka laptop dulu. Bicara saja.

**Naskah:**

> "PKL sekarang bukan lagi kegiatan tambahan. Sejak Kepmendikbudristek 262/M/2022, PKL
> jadi mata pelajaran wajib — minimal 6 bulan, 792 jam pelajaran, di kelas XII. Sekitar
> 14 ribu SMK wajib merencanakan, memantau, menilai, dan mendokumentasikannya seperti
> mapel biasa.
>
> Tapi di lapangan? Buku jurnal kertas dan grup WhatsApp.
>
> Akibatnya begini: seorang siswa kerja 6 bulan di industri, lalu lulus tanpa satu pun
> bukti kompetensi yang bisa dia tunjukkan waktu melamar. Enam bulan itu hilang.
>
> Vokasia menjawab satu pertanyaan: **bagaimana 6 bulan kerja nyata bisa berubah jadi
> bukti yang bisa diverifikasi orang lain?**"

**Aturan babak ini:**

- Satu kalimat masalah, satu kalimat akibat, satu kalimat pertanyaan. Berhenti.
- Jangan sebut teknologi sama sekali di sini.
- Angka regulasi & statistik → siapkan slide sumber. Kalau ditanya "datanya dari mana",
  kamu harus bisa jawab dalam 3 detik. Kalau belum punya sumbernya, **hapus angkanya**
  dan bilang kualitatif saja.

---

## 2. BABAK 2 — ALUR (8 menit) ← INI INTINYA

### Prinsip

Jangan tur menu. **Ikuti satu orang.** Beri nama. Sebut jurusannya.

> "Namanya Rina. Kelas XII TKJ. PKL 6 bulan di sebuah bengkel komputer."

Setiap layar yang kamu buka harus menjawab: *"lalu Rina bagaimana?"*
Kalau sebuah layar tidak menjawab itu, lewati.

### Rantai alur (hafalkan urutannya — ini tulang punggung produk)

```
Periode  →  Placement  →  Jurnal harian  →  Approval mentor
   →  Monitoring guru  →  Penilaian rubrik  →  Sertifikat ber-QR  →  Portofolio publik
```

Tiap tahap punya pemilik yang berbeda. Itu sebabnya ada 4 surface, bukan karena ingin
banyak halaman.

### Naskah per tahap

| # | Yang kamu tunjukkan | Kalimat yang diucapkan | Poin tersembunyi |
|---|---|---|---|
| 1 | `/app` — admin bikin periode, import siswa CSV | "Waka Hubin buka periode PKL, impor 300 siswa dari CSV Dapodik." | Data sekolah tidak diketik ulang |
| 2 | `/app` — placement: siswa → DUDI → guru → mentor | "Tiap siswa dipasangkan ke DUDI, guru pembimbing, dan mentor industri." | Ini simpul datanya — semua nempel di sini |
| 3 | Email mentor (magic link) | "Mentor industri **tidak dibuatkan password**. Dia dapat link sekali pakai, klik, langsung masuk." | Keputusan desain sadar — mentor tidak digaji sekolah, jangan dibebani akun |
| 4 | `/student` jam 05:00 — slot jurnal muncul | "Tiap pagi sistem menyiapkan slot jurnal, otomatis melewati hari libur." | Siswa tidak perlu tahu hari apa |
| 5 | `/student` — Rina isi jurnal | "Teks singkat, pilih kompetensi, foto. Target: di bawah 2 menit di HP Android murah, sinyal 3G." | Kalau lebih dari 2 menit, tidak akan diisi |
| 6 | Jam 19:00 — reminder | "Belum isi? Jam 7 malam diingatkan." | |
| 7 | Jam 21:00 — status MERAH | "Tiga hari kerja kosong berturut-turut → statusnya jadi merah, guru dan admin langsung tahu." | **Tunjukkan ini pelan-pelan.** Ini fitur yang paling laku ke sekolah |
| 8 | `/mentor` — batch approve | "Mentor approve sepuluh jurnal sekaligus, sekali seminggu. Setelah di-approve, jurnal terkunci — tidak bisa diedit siapa pun." | Immutability = bukti bisa dipercaya |
| 9 | `/app` — kunjungan monitoring guru | "Guru datang ke lokasi, isi form di HP: catatan, foto, tanda tangan." | |
| 10 | H-14 — fase penilaian buka sendiri | "Dua minggu sebelum periode selesai, fase penilaian terbuka otomatis." | Tidak ada yang perlu ingat |
| 11 | Penilaian dua sisi | "Mentor menilai aspek industri, guru menilai aspek sekolah. Skornya berbobot menurut rubrik." | Nilai tidak datang dari satu sudut pandang |
| 12 | Admin finalisasi | "Admin finalisasi. Nilai terkunci." | |
| 13 | Sertifikat PDF ber-QR | "Sertifikat digenerate — identitas, DUDI, durasi, nilai, plus QR." | |
| 14 | **Scan QR-nya beneran pakai HP** | "Ini yang penting. Siapa pun bisa scan QR ini dan memverifikasi." | ← **Momen puncak presentasi** |
| 15 | `/p/rina` — portofolio publik | "Kalau Rina mau, dia publish portofolionya. Kompetensi, sampel kegiatan yang sudah di-approve, sertifikatnya. Tanpa NISN, tanpa nomor HP." | Opt-in + data anak dilindungi |

### Momen puncak: scan QR di depan penonton

Ini satu-satunya bagian presentasi yang **wajib** dilakukan live dengan HP asli.
Alasannya: seluruh cerita Babak 1 ("6 bulan tanpa bukti") baru benar-benar tertutup
di detik penonton melihat halaman verifikasi terbuka dari QR.

Latih ini sampai lancar. Siapkan sertifikat contoh yang QR-nya sudah diuji.

---

## 3. BABAK 3 — TEKNOLOGI (5 menit)

### Aturan mutlak

**Jangan sebut satu teknologi pun tanpa menyebut kendala yang memaksanya.**
Daftar teknologi tanpa alasan = penonton teknis akan langsung bertanya "kenapa bukan X",
dan kamu akan bertahan, bukan menjelaskan.

Formatnya selalu: **kendala → pilihan → konsekuensi.**

### Naskah

**(a) Kenapa ada antrian pesan, bukan langsung diproses**

> "Perhatikan jam berapa siswa isi jurnal: pulang PKL, sekitar jam 4 sampai 8 malam.
> Semua serempak. Ratusan siswa, masing-masing bawa sampai 3 foto 5MB.
>
> Kalau kompresi foto dikerjakan di request yang sama, API-nya mati.
>
> Jadi request-nya cuma menyimpan dan mengembalikan 'sudah diterima'. Sisanya —
> kompres foto, hapus GPS dari EXIF, buat thumbnail, hitung streak, update status —
> masuk antrian, dikerjakan worker terpisah."

Teknologi yang keluar di sini: **RabbitMQ + MassTransit + Worker terpisah.**

**(b) Kenapa transactional outbox**

> "Masalahnya: kalau data sudah tersimpan tapi pesan ke antrian gagal terkirim — foto
> tidak pernah diproses, dan tidak ada yang tahu. Jadi pesannya ditulis ke tabel yang
> sama, dalam transaksi yang sama dengan datanya. Baru dikirim belakangan. Event tidak
> bisa hilang."

Kalau ditanya "kenapa tidak langsung publish saja" → itu pertanyaan bagus, jawab:
"karena simpan-ke-DB dan kirim-ke-broker bukan satu transaksi. Salah satu bisa gagal."

**(c) Kenapa ada cron terjadwal**

> "Sebagian kejadian tidak dipicu manusia — dipicu waktu. Slot jurnal jam 5 pagi.
> Reminder jam 7 malam. Deteksi ghosting jam 9 malam. Fase penilaian H-14. Invoice
> tanggal 1. Semuanya zona waktu Asia/Jakarta eksplisit, bukan UTC server."

Teknologi: **Hangfire.** Ada 6 cron job di kode.

**(d) Kenapa token tidak disimpan di browser**

> "Ini aplikasi yang memegang data anak di bawah umur. Jadi browser tidak pernah
> memegang token — cuma cookie httpOnly. Token asli disimpan di sisi server, di Redis.
> Frontend jadi perantara yang menempelkan token ke tiap panggilan API."

Teknologi: **OpenIddict + pola BFF + Redis.**

**(e) Kenapa multi-tenant dengan filter di ORM**

> "Satu sistem dipakai banyak sekolah. Kalau filter sekolah ditulis manual di tiap
> query, satu kali lupa = data sekolah A bocor ke sekolah B. Jadi filternya dipasang
> di level ORM — otomatis, tidak bisa lupa.
>
> Kecuali mentor. Mentor industri bisa menerima siswa dari beberapa sekolah sekaligus,
> jadi mentor difilter per penempatan, bukan per sekolah."

Teknologi: **EF Core global query filter.**

### Slide ringkas stack (tampilkan di akhir babak, bukan awal)

| Lapis | Pilihan |
|---|---|
| Backend | C# .NET 10 LTS |
| Frontend | Next.js 16 (App Router, PPR), runtime Bun |
| Database | PostgreSQL 17 |
| Cache & sesi | Redis 7 |
| Antrian | RabbitMQ 3 + MassTransit |
| Penjadwalan | Hangfire |
| Penyimpanan file | MinIO (S3) |
| Auth | OpenIddict (OAuth2 + PKCE) + pola BFF |
| PDF | QuestPDF |
| Deploy | Docker Compose, 7 container, 1 VPS |

**Kalau ditanya "kenapa .NET, bukan Node/Laravel?"** — jawab jujur, jangan mengarang
keunggulan: *"Karena beban terberat sistem ini ada di worker latar belakang, bukan di
request. Ekosistem background job dan messaging di .NET matang, dan tipenya ketat —
buat solo developer, compiler yang cerewet itu penghemat waktu."*

---

## 4. BABAK 4 — CARA BIKIN (4 menit)

Ini bagian paling khas dari proyek ini. Jangan diperlakukan sebagai catatan kaki.

### Naskah

> "Saya kerjakan sendirian. Tapi bukan mengetik sendirian.
>
> Saya jalankan proyek ini seperti tim kecil dengan pembagian peran yang tegas:
>
> — Saya: pemilik keputusan. Satu-satunya yang boleh merge dan deploy.
> — Satu AI berperan Product Manager dan code reviewer. Tidak menulis kode fitur sama
>   sekali. Tugasnya memecah pekerjaan jadi tiket dengan kriteria penerimaan, dan
>   menolak setiap perubahan yang tidak lolos review.
> — Tiga AI berperan engineer, dengan **wilayah kerja yang dikunci**: satu backend,
>   satu frontend, satu keamanan. Yang backend dilarang menyentuh frontend, dan
>   sebaliknya.
>
> Kenapa dikunci? Karena tanpa batas wilayah, mereka saling menimpa pekerjaan."

### Empat aturan yang bikin ini jalan (sebut ini — ini isinya)

1. **Satu sumber kebenaran.** PRD adalah kontrak. Setiap keputusan yang menyimpang
   dicatat di log keputusan, bernomor, berurutan. Tidak ada keputusan diam-diam.

2. **"Sudah ditest" tidak dipercaya.** Reviewer menjalankan build dan test sendiri.
   Klaim tanpa output test asli ditolak.

3. **Test harus dibuktikan bisa merah.** Setiap test baru: rusak dulu implementasinya,
   pastikan test-nya gagal, kembalikan, pastikan lulus. Test yang tidak pernah terbukti
   bisa merah bukan test — itu dekorasi.

4. **Ide baru saat sprint ditolak**, masuk backlog. Scope dikunci.

### Angka yang boleh kamu sebut (sudah diverifikasi dari repo, 31 Juli 2026)

| Metrik | Angka |
|---|---|
| Baris kode backend (C#) | ~22.500 |
| Baris kode frontend (TS/TSX) | ~12.300 |
| File test | 86 |
| Method test (`[Fact]`/`[Theory]`) | 304 (3 di-skip, terdokumentasi alasannya) |
| Halaman frontend | 26 route |
| Grup endpoint API | 19 file |
| Consumer antrian | 13 |
| Cron job terjadwal | 6 |
| Migration database | 6 |
| Container di produksi | 7 |
| Tiket sprint | 21 (H1–H7) |

> Angka-angka ini dihitung langsung dari repo. Kalau ada yang minta bukti, buka
> terminal dan hitung ulang di depan mereka. Itu justru poin plus.

---

## 5. YANG TIDAK BOLEH KAMU KLAIM

Diperiksa terhadap kode. Kalau kamu sebut ini dan ada yang mengecek, kamu kehilangan
kredibilitas untuk seluruh presentasi.

| Klaim | Status | Yang boleh dikatakan |
|---|---|---|
| "E2E Playwright 5 persona" | ❌ Tidak ditemukan file `.spec.ts`, Playwright tidak ada di `package.json` | "E2E belum dikerjakan, itu tiket H7-E3 yang tersisa." |
| "Sudah dipakai 3 SMK" | ❌ Pilot belum jalan | "Target pilot 3 SMK. Belum mulai." |
| "Uptime 99%" | ❌ Belum ada produksi berjalan | "Targetnya 99% dengan topologi 1 VPS." |
| "Lighthouse skor X" | ❌ Belum ada bukti terukur di repo | Lewati saja |
| "Semua test hijau" | ⚠️ Harus dijalankan hari ini sebelum diklaim | Jalankan `dotnet test` pagi hari presentasi |

**Aturan umum:** kalau belum dieksekusi, katakan **"belum"** — bukan "hampir" atau
"sudah tinggal sedikit". Orang teknis menghormati "belum". Mereka tidak menghormati
kabur.

**Catatan:** `HANDOFF-NEXT-SESSION.md` di repo sudah usang (menyebut sprint di H4-E1),
padahal git log menunjukkan H6-E2 selesai dan ada commit "Production Ready". Jangan
presentasi dari file itu.

---

## 6. PERTANYAAN SULIT & JAWABANNYA

### Dari kalangan teknis

**"Kenapa 7 container untuk MVP? Overkill."**
> "Untuk MVP, iya, bisa lebih sedikit. Tapi burst jam 4–8 malam itu nyata, dan generate
> ratusan PDF tidak boleh memblokir API. Yang saya hindari bukan biaya container —
> tapi menulis ulang arsitektur saat sekolah ke-10 masuk."

**"Multi-tenant satu database — bukannya berisiko?"**
> "Ya, kalau filternya manual. Makanya filternya di level ORM, dan ada test isolasi
> tenant khusus. Risiko yang tersisa: kalau seseorang menulis raw SQL yang melewati
> ORM. Itu yang dijaga di review."

**"Kenapa tidak pakai Supabase/Firebase saja, lebih cepat?"**
> "Untuk auth doang, iya. Tapi yang berat di sini justru yang tidak disediakan mereka:
> outbox transaksional, cron zona waktu, worker generate PDF. Saya tetap harus bangun
> lapisan itu. Jadi tidak ada yang dihemat."

**"AI yang nulis kodenya. Kamu paham kodenya?"**
> Ini pertanyaan paling mungkin muncul. Jangan defensif.
> "Saya yang mereview dan yang merge. Tidak ada satu baris pun masuk tanpa saya
> setujui. Silakan tunjuk file mana saja, saya jelaskan kenapa begitu."
> **Lalu pastikan kamu memang bisa.** Kuasai minimal: alur auth BFF, cara kerja outbox,
> dan satu consumer dari awal sampai akhir.

### Dari kalangan sekolah / bisnis

**"Berapa harganya?"**
> Siapkan angka. Jangan improvisasi di depan orang.

**"Kalau internet di sekolah kami jelek?"**
> "Halaman siswa dirancang di bawah 200KB, target HP Android murah dan 3G. Antrian
> offline ada di backlog, belum masuk MVP." — jujur soal yang belum ada.

**"Data siswa kami aman? Ada anak di bawah umur."**
> "Portofolio publik itu opt-in, dan tidak memuat NISN atau kontak. GPS dihapus dari
> foto secara default. Ada retensi foto 2 tahun dan hak hapus saat lulus." — ini
> pertanyaan yang paling sering datang dari kepala sekolah. Hafalkan.

**"Kalau mentor industrinya tidak mau pakai?"**
> Pertanyaan paling berbahaya, karena ini risiko produk yang nyata.
> "Itu risiko terbesar produk ini, dan sebabnya mentor tidak dibuatkan password sama
> sekali. Klik link di email, langsung masuk, approve sepuluh jurnal sekaligus,
> seminggu sekali. Kalau tetap tidak jalan, ya asumsi saya salah — itu yang mau saya
> uji di pilot."
> **Jawaban ini kuat justru karena mengakui risikonya.**

---

## 7. VERSI 10 MENIT (kalau waktunya dipotong)

Buang Babak 4 sampai jadi satu kalimat. Jangan pernah buang Babak 2.

- Babak 1 — masalah · **1 menit**
- Babak 2 — alur, tapi lompat ke tahap 1, 5, 7, 8, 13, 14, 15 saja · **6 menit**
- Babak 3 — cuma poin (a) antrian dan (d) keamanan · **2 menit**
- Babak 4 — satu kalimat: "Solo, dengan AI sebagai tim berperan, 21 tiket, 7 hari sprint." · **1 menit**

---

## 8. CHECKLIST PAGI HARI PRESENTASI

- [ ] `docker compose up -d` → tunggu semua sehat
- [ ] Seed data jalan — cek ada siswa contoh dengan jurnal 90 hari
- [ ] Pastikan ada satu siswa berstatus **MERAH** di data demo (tahap 7 butuh ini)
- [ ] Pastikan ada satu sertifikat jadi + QR-nya **sudah diuji scan pakai HP**
- [ ] `dotnet test` → catat hasilnya, jangan klaim hijau tanpa lihat
- [ ] Screenshot 8 layar kunci sebagai cadangan kalau demo mati
- [ ] Login semua role sekali, pastikan tidak ada yang expired
- [ ] Zoom browser 125% — penonton di belakang tidak bisa baca 14px
- [ ] Matikan notifikasi desktop

---

## 9. TIGA KESALAHAN YANG PALING MUNGKIN KAMU LAKUKAN

1. **Tur menu, bukan alur.** Begitu kamu bilang "ini ada menu X, ada menu Y", kamu
   sudah kehilangan mereka. Kembali ke Rina.

2. **Sebut teknologi tanpa kendalanya.** "Saya pakai RabbitMQ" itu tidak menarik.
   "Ratusan siswa upload foto serempak jam 5 sore" itu menarik — dan RabbitMQ jadi
   jawaban yang jelas.

3. **Menghaluskan yang belum jadi.** Katakan "belum". Setiap kali. Presentasi teknis
   dinilai dari kalibrasi, bukan dari klaim.
