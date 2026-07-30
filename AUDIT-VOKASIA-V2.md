# Audit Vokasia v2 — Baseline Baru Pasca-Perbaikan

Audit independen baru, ditulis dari nol terhadap kondisi kode dan aplikasi **saat ini** (bukan revisi dari audit sebelumnya). Ini bukan audit pertama untuk Vokasia — dua audit sebelumnya menemukan sejumlah gap (login OAuth tanpa gaya, kemungkinan open redirect, 404 generik, tap target di bawah standar, dsb), dan Developer melaporkan semuanya sudah diperbaiki. Audit ini memverifikasi ulang dari nol, ditambah pencarian temuan baru yang belum pernah diangkat.

## Metodologi

- Observasi live terhadap instance yang berjalan di Docker (`localhost:3000` frontend Next.js, `localhost:5000` backend OAuth .NET), lewat browser sungguhan.
- Pembacaan langsung kode sumber di `D:\Web\Vokasia` (frontend Next.js 16/React 19, backend .NET 10) — bukan opini, setiap klaim diverifikasi ke file dan baris.
- Dicek terhadap kontrak desain proyek sendiri (`DESIGN.md`) dan PRD (`PRD.md`), bukan selera pribadi auditor.
- **Keterbatasan yang berlaku sama seperti audit sebelumnya**: sandbox saya tidak bisa memaksa resize viewport Chrome ke lebar mobile sungguhan (`window.innerWidth` tetap mengikuti ukuran layar fisik walau `resize_window` dipanggil) — jadi klaim mobile-friendliness bersumber dari pembacaan kode (breakpoint, `--tap-min`, `md:hidden`), bukan screenshot 375px asli. Login dengan kredensial (untuk melihat dashboard `/app` `/mentor` `/sa`/isi `/student`) juga tidak saya coba — baik karena tidak relevan untuk audit publik-facing, maupun karena upaya submit form login sebelumnya diblokir classifier keamanan saya sendiri.

---

## 1. Ringkasan eksekutif

Vokasia — platform manajemen PKL (Praktik Kerja Lapangan) untuk SMK, multi-tenant SaaS — berada dalam kondisi **jauh lebih matang** dari audit pertama saya. Halaman publik yang sempat jadi titik lemah paling mencolok (login OAuth tanpa gaya sama sekali, 404 default Next.js polos-hitam) sekarang konsisten penuh dengan sistem desain (`DESIGN.md`, token OKLCH tema "sekolah"). Saya verifikasi ini langsung lewat browser, bukan hanya membaca kode.

Disiplin kode tetap tinggi: primitif UI dasar (`Input`, `ErrorState`, `EmptyState`) semuanya punya `<label>` asli, `role="alert"`, slot error yang dicadangkan supaya tidak ada layout shift, dan prinsip "tidak pernah layar buntu" ditegakkan konsisten — bukan cuma slogan di `DESIGN.md`, tapi benar-benar diterapkan di komponen dasar yang dipakai berulang.

Temuan audit ini condong ke hal-hal yang lebih halus/spesifik dibanding audit pertama — bukan karena saya kurang teliti, tapi karena isu-isu besar memang sudah selesai. Yang tersisa didominasi oleh tiga area lama yang belum tersentuh (isolasi tenant lewat header `X-Acting-Tenant`, filter tenant yang belum mencakup semua entity, healthcheck worker yang dangkal) plus beberapa temuan baru yang saya angkat di bawah.

---

## 2. Apa yang sudah baik (terverifikasi ulang dari nol)

- **Halaman login** (`localhost:5000/account/login`) — dibuka live: tema "sekolah" penuh, `<label>` asli, kartu bermerek Vokasia, tap target 44px, `prefers-reduced-motion` dihormati.
- **Halaman verifikasi sertifikat gagal** (`/verify/BADCODE1`) — dibuka live dengan kode acak: kartu error jelas, breadcrumb "Periksa kode lain" di atas, tombol "Kembali ke beranda" di bawah dengan ikon rumah, footer bermerek "Verifikasi publik Vokasia". Ini melampaui apa yang secara eksplisit diklaim diperbaiki — sepertinya ikut terbenahi sebagai bagian dari sapuan desain menyeluruh, bukan tempelan satu halaman.
- **Landing page** — copy sudah diperbarui ("Proses PKL yang tertib, dari jurnal sampai kompetensi"), struktur 3 langkah bernomor tetap konsisten dengan tema institusional 222° yang dipertahankan.
- **Primitif UI dasar** (`Input.tsx`, `ErrorState.tsx`, `EmptyState.tsx`) — label asli (bukan placeholder-only), `aria-describedby` terhubung ke pesan error/hint, slot error punya `min-height` tetap supaya form tidak "melompat" saat validasi gagal muncul, `ErrorState` selalu punya tombol retry (tidak pernah dead-end).
- **Ikon SVG konsisten** — `Icon.tsx` dipakai di seluruh navigasi dan komponen interaktif yang sempat memakai emoji (`PhotoUploader`), tanpa sisa emoji fungsional yang saya temukan di pass baru ini.

---

## 3. Temuan baru — UX

**Copy landing page dan copy `/login` tidak lagi 100% sinkron.** Landing sekarang bilang "Gunakan akun siswa, mentor, atau staf yang diberikan sekolah maupun pengelola Vokasia" — sementara kartu `/login` (Next.js, sebelum redirect ke backend) bilang "Gunakan akun yang diberikan sekolah atau pengelola Vokasia" (tanpa menyebut "siswa, mentor, atau staf"). Bukan bug, tapi dua kalimat yang seharusnya identik (keduanya menjelaskan hal yang sama, satu halaman sebelum yang lain) sekarang sedikit berbeda kata — kesempatan kecil untuk konsistensi copy kalau ada perubahan lanjutan di salah satu.

**Halaman verifikasi sertifikat sukses belum saya lihat** — saya hanya bisa menguji kasus gagal (kode salah), karena tidak punya kode sertifikat asli untuk diuji. Kasus sukses (`/verify/{kode-valid}`) kemungkinan sudah sama baiknya berdasarkan pola kode yang konsisten, tapi ini tetap satu jalur yang belum benar-benar saya verifikasi visual — soal QA sertifikat sungguhan sebaiknya dicek manual sebelum rilis.

**Belum ada indikasi kekuatan kata sandi atau show/hide password di form login.** Form login sudah bagus (label asli, validasi returnUrl, antiforgery), tapi field password polos tanpa toggle "tampilkan kata sandi" — untuk pengguna HP (siswa SMK, target eksplisit "Android murah" di `DESIGN.md`) mengetik password di keyboard virtual tanpa bisa memverifikasi apa yang diketik adalah sumber salah-ketik yang umum, terutama untuk password demo yang panjang dan campuran karakter (`Demo-Passw0rd!`).

---

## 4. Temuan baru — UI & mobile

**Warna ikon status pada kartu error verifikasi.** Di `/verify/BADCODE1`, lingkaran ikon ✕ memakai kombinasi merah-di-atas-merah-muda (`status-red` di atas `status-red-bg`) — kontrasnya terlihat cukup dari screenshot, tapi ini satu tempat yang layak diverifikasi terukur (bukan cuma visual) mengingat proyek ini sangat disiplin soal verifikasi WCAG terprogram di tempat lain (`globals.css` mendokumentasikan rasio kontras eksplisit) — kartu error verifikasi sepertinya belum melalui verifikasi yang sama secara terdokumentasi.

**Belum ada breadcrumb/navigasi "kembali" yang konsisten di semua halaman publik.** `/verify/[code]` sekarang punya "Periksa kode lain", `/p/[slug]` (404) punya "Ke beranda" + "Masuk ke Vokasia" — tapi dua pola ini agak berbeda (breadcrumb di atas vs tombol besar di bawah). Bukan salah, tapi kalau ada halaman publik baru ke depan, ada baiknya satu pola dijadikan standar eksplisit di `DESIGN.md` supaya tidak mengembang jadi variasi tak terkendali.

**Mobile-friendliness tetap tidak bisa saya verifikasi visual** (lihat batasan metodologi). Dari kode: `--tap-min: 44px` dipakai konsisten di semua tempat yang saya baca ulang kali ini (`Input`, form login backend, `not-found.tsx`, `PhotoUploader`) — tidak ada regresi yang saya temukan dari sisi ini.

---

## 5. Temuan baru — kode & arsitektur

**Duplikasi logika validasi `returnUrl`.** Sekarang ada di dua tempat terpisah dengan implementasi yang mirip tapi tidak identik: `backend/src/Vokasia.Api/Auth/AccountEndpoints.cs` (`GetSafeReturnUrl`, mengecek `'/'`, `'//'`, backslash, karakter kontrol) dan `frontend/src/lib/localReturnUrl.ts` (`getSafeLocalReturnUrl`, logika serupa tapi ditulis ulang di TypeScript). Keduanya benar secara terpisah, tapi karena ini logika keamanan (anti open-redirect), dua implementasi paralel di dua bahasa berarti kalau suatu saat satu diperbarui (misal menambah pengecualian baru) dan yang lain lupa, keduanya bisa diam-diam berbeda perilaku. Bukan urgent, tapi worth dicatat sebagai item "jaga tetap sinkron" — mungkin pantas ditulis sebagai catatan eksplisit di kedua file yang saling menunjuk satu sama lain (saat ini belum ada catatan silang).

**Komponen dasar (`EmptyState`, `ErrorState`) tidak punya test unit sendiri**, meski dipakai di hampir semua list/halaman di aplikasi (disebut eksplisit di komentar sebagai fondasi "NFR-UX-04"). Mengingat betapa sering primitif ini dipakai ulang, satu test snapshot/render sederhana untuk masing-masing akan murah untuk ditulis dan mengurangi risiko regresi diam-diam kalau ada yang mengubahnya untuk kebutuhan komponen lain.

**Tiga item lama masih terbuka, saya verifikasi ulang dan masih sama persis:**
- `TenantResolutionMiddleware.cs` — header `X-Acting-Tenant` (SuperAdmin melonggarkan filter tenant) masih tanpa audit-log per-query.
- `VokasiaDbContext.cs` — `ApplyTenantQueryFilters()` masih belum mencakup `TenantCompany`, `CompanySlot`, `Invoice`.
- `docker-compose.yml` — healthcheck worker masih `pgrep dotnet || exit 1`, mengakui sendiri di komentar bahwa ini tidak memverifikasi koneksi broker.

Ini tiga satu-satunya kelompok temuan berulang yang menyentuh **isolasi data lintas tenant** — beda kelas dari temuan UX/UI di atas, dan tetap jadi prioritas paling masuk akal sebelum rilis produksi multi-sekolah sungguhan.

---

## 6. Yang tidak berubah dari audit sebelumnya (masih akurat)

Inventaris fitur (jurnal siswa, approval mentor, dashboard RAG sekolah, kunjungan+tanda tangan digital, penilaian & rekap berbobot, sertifikat PDF ber-QR + verifikasi publik, portofolio publik, panel superadmin lengkap) tidak saya ulang di sini — itu tidak berubah sejak audit sebelumnya dan sudah terdokumentasi lengkap di `AUDIT-HALLMARK-UX-UI-KODE.md` §6 pada folder yang sama. Rujuk ke situ untuk daftar fitur lengkap per peran.

---

## 7. Prioritas (audit ini)

1. **Isolasi tenant** (§5, tiga item) — belum berubah dari audit sebelumnya, tetap prioritas tertinggi untuk rilis produksi multi-sekolah.
2. **Toggle tampilkan/sembunyikan password** di form login — kecil, tapi relevan langsung untuk target pengguna HP.
3. **Selaraskan copy landing vs `/login`** soal siapa saja yang bisa pakai akun (siswa/mentor/staf).
4. **Verifikasi kontras terukur untuk kartu status error/sukses `/verify`** — ikuti pola yang sudah dipakai proyek ini sendiri di `globals.css` (rasio WCAG terdokumentasi, bukan cuma dicek mata).
5. **Uji manual jalur `/verify/{kode-valid}`** (kasus sukses) — belum pernah diverifikasi visual di audit mana pun karena butuh kode sertifikat asli.
6. Catat silang `GetSafeReturnUrl` (backend) dan `getSafeLocalReturnUrl` (frontend) satu sama lain di komentar, supaya kalau satu diperbarui, yang lain tidak lupa menyusul.
7. Tambahkan test render sederhana untuk `EmptyState`/`ErrorState` mengingat seberapa luas dipakai ulang.
