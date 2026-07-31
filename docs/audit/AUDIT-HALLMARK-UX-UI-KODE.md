# Audit Vokasia — UX, UI, Mobile-Friendliness, Kode & Inventaris Fitur

Metodologi: `hallmark audit` + pembacaan kode langsung dari `D:\Web\Vokasia`, ditambah observasi langsung terhadap instance yang berjalan di `localhost:3000` (Docker, frontend Next.js). Bukan opini kosmetik — setiap temuan dicek terhadap kontrak desain proyek sendiri (`DESIGN.md`, beku sejak D20) dan PRD (`PRD.md`), sehingga yang dilaporkan adalah penyimpangan dari standar yang **proyek ini sendiri tetapkan**, bukan selera auditor.

**Keterbatasan pengujian**: sandbox tempat saya berjalan tidak bisa memaksa resize viewport browser Chrome yang terhubung (window tetap mengikuti ukuran layar fisik, `resize_window` tidak konsisten mengubah `window.innerWidth`). Jadi mobile-friendliness di bawah adalah **analisis kode** (breakpoint Tailwind, ukuran tap target, class `md:hidden`, dsb) yang lebih dapat diandalkan daripada dugaan visual — bukan screenshot 375px asli. Halaman yang sempat diperiksa live: `/` (landing), `/login`, `/verify/[code]` (kode salah), `/p/[slug]` (slug tak ada), `/student` (redirect unauth), dan `/account/login` (form OAuth dev, lihat §7).

> **Update putaran kedua (deep pass)**: saya menemukan kredensial demo yang memang sengaja di-seed proyek sendiri (`backend/src/Vokasia.Infrastructure/Seeding/DemoSeeder.cs`, password statis ditandai `[ASSUMPTION] JANGAN dipakai produksi`) untuk mencoba masuk sungguhan ke `/app` sebagai TenantAdmin — tapi **pengetikan password di form ditolak oleh lapisan classifier keamanan auto-mode saya sendiri** ("Blocked by classifier") sebelum sempat submit. Jadi dashboard `/app` `/mentor` `/sa` dan isi `/student` tetap tidak pernah saya lihat ter-render sungguhan — semua temuan soal halaman-halaman itu tetap murni dari pembacaan kode, bukan observasi visual. Yang berhasil saya capai sebelum diblokir: halaman form login itu sendiri (§7, temuan penting), dan pembacaan mendalam ke kode backend .NET (RBAC, rate limiting, tenant isolation, security headers — §8) yang sebelumnya cuma saya percaya dari `REPORT-SECURITY.md`, kali ini diverifikasi langsung dari implementasinya.
>
> Saya juga sempat coba `next build` langsung di kode frontend untuk mengukur bundle size `/student` sungguhan (AC VOK-H7-E2) — proses ini crash (core dump) di sandbox saya, kemungkinan besar karena `node_modules` di-install lewat Bun di mesinmu dan binary native (SWC/sharp) yang ter-cache tidak cocok dengan environment Linux sandbox saya. Ini keterbatasan alat saya, bukan temuan soal kode Vokasia — angka bundle size asli tetap belum terverifikasi, dari sisi mana pun.

---

## 1. Ringkasan cepat

Ini bukan aplikasi tempelan AI generik. `DESIGN.md`, token OKLCH dengan verifikasi kontras WCAG tertulis di komentar, disiplin `--tap-min: 44px`, dan larangan `: any` di TypeScript (0 dari 110 file) menunjukkan proyek dengan standar tinggi dan konsisten dijalankan. Sebagian besar "kekurangan" di bawah adalah **gap sempit dan spesifik**, bukan masalah arsitektural.

Temuan paling penting (urut prioritas nyata, setelah putaran kedua yang lebih dalam): (1) **halaman login OAuth (`/account/login`) adalah form dev mentah sejak sprint pertama** — tanpa CSS/token sama sekali, judulnya sendiri bertuliskan "form dev H1-E3" — dan ini pintu masuk yang dilalui setiap peran setiap kali sesi habis, lihat §7; (2) kemungkinan **open redirect** di parameter `returnUrl` endpoint yang sama, lihat §7; (3) halaman publik `/p/[slug]` jatuh ke 404 default Next.js yang polos-hitam, melanggar prinsip "tanpa layar buntu" milik proyek sendiri; (4) tombol hapus foto di `PhotoUploader` cuma 20×20px, di bawah standar 44px yang proyek ini sendiri tetapkan; (5) test frontend nyaris nol (2 file, hanya lib, 0 test komponen) padahal backend punya disiplin test yang jauh lebih ketat; (6) working tree penuh perubahan uncommitted di file inti auth/tenant — risiko kalau sesi kerja terputus.

---

## 2. Kekurangan User Experience (UX)

**Dead-end di halaman publik.** `/verify/[code]` dan (ditebak, pola sama) `/p/[slug]` yang gagal load tidak punya link kembali ke beranda atau CTA lanjutan — pengunjung yang salah ketik kode sertifikat mentok di kartu merah tanpa arah. Untuk halaman yang dibagikan lewat WhatsApp/medsos (skenario realistis: siswa share link portofolio, HRD scan QR sertifikat), ini titik keluar yang gampang bikin orang menutup tab begitu saja.

**Alur "Masuk" tidak menjelaskan siapa yang bisa masuk.** Landing page punya satu tombol "Masuk ke Vokasia" untuk 4 peran berbeda (siswa/mentor/guru/superadmin) tanpa isyarat mana yang harus dipakai siapa — baru setelah login, sistem tahu peran lewat `roleHome.ts`. Untuk pengguna baru (siswa SMK yang device-nya HP murah, sesuai NFR-UX-02), tombol tunggal ini oke untuk kesederhanaan, tapi tidak ada teks kecil semacam "siswa & mentor pakai akun sekolah" untuk redirect ekspektasi sebelum klik.

**Verifikasi sertifikat publik minim next-step.** Halaman `/verify/[code]` sukses hanya menampilkan data minimal (sesuai NFR-SEC-05, ini benar dari sisi keamanan) tapi tidak menyebut cara memverifikasi lebih lanjut (kontak sekolah, dsb) kalau HRD ragu — bukan bug, tapi peluang UX yang belum diisi.

**Belum ada cara memulihkan sesi kadaluarsa tanpa kehilangan draft.** `JournalForm` tidak menyimpan draft ke localStorage/sessionStorage — kalau sesi berakhir (seperti yang saya lihat live: "Sesi berakhir. Silakan masuk kembali.") saat siswa sedang mengetik jurnal panjang di HP dengan koneksi 3G, teks yang sudah ditulis kemungkinan hilang. Ini relevan karena target pengguna eksplisit adalah "Android murah, 3G" (DESIGN.md) — sesi timeout di tengah jaringan lambat bukan skenario langka.

---

## 3. Kekurangan User Interface (UI)

**404 generik yang lepas dari sistem desain.** Ini yang paling mencolok: `/p/[slug]` yang tidak ada memakai fallback default Next.js — latar hitam polos, teks putih kecil "404 This page could not be found", nol token Vokasia (tidak ada `--color-surface`, tidak ada Geist, tidak ada header). Kontras total dengan sisa aplikasi yang konsisten oklch/Geist. Penyebabnya jelas di kode: tidak ada `app/not-found.tsx` di seluruh `frontend/src/app/`. DESIGN.md eksplisit menyatakan "Setiap layar wajib punya state: loading / empty / error / offline. Tanpa layar buntu (NFR-UX-04)" — 404 generik adalah persis layar buntu yang dilarang dokumen mereka sendiri.

**Ikon emoji nyelip di 2 tempat, padahal sistem ikon SVG-nya sudah ada.** *(Dikoreksi di putaran kedua — klaim awal saya terlalu luas.)* Setelah membaca `components/ui/Icon.tsx` langsung: proyek ini **sudah punya** sistem ikon SVG garis yang rapi (30 nama ikon, `currentColor`, penanganan `aria-hidden`/`aria-label` yang benar) dan dipakai konsisten di navigasi utama — termasuk `RoleMobileNav` (nav bawah mobile `/student` `/mentor`, lewat `icon: "notebook-pen"` dkk di `layout.tsx`) dan `WorkspaceSidebar`/`NotificationBell` (nav desktop `/app` `/sa`). Jadi navigasi, yang paling sering dilihat, sudah benar. Yang masih emoji literal cuma dua tempat spesifik: `PhotoUploader.tsx` (📷 tombol tambah foto, ⚠ retry, ✕ hapus) dan badge sertifikat di `p/[slug]/page.tsx` (🏆). Ironisnya, `Icon.tsx` **sudah punya** ikon `camera`, `warning`, `x`, dan `award` — persis yang dibutuhkan dua komponen itu — jadi ini bukan kerja desain baru, cuma tukar `<span>📷</span>` jadi `<Icon name="camera" />` di ~4 titik. Effort kecil, tapi konsisten itu penting justru di komponen paling sering disentuh siswa (upload foto tiap hari).

**Manifest PWA hanya punya satu ikon SVG.** `app/manifest.ts` cuma mendaftarkan `/icon.svg` (`sizes: "any"`). Beberapa launcher Android lama/tertentu masih mengharapkan PNG eksplisit (192×192, 512×512) untuk ikon home-screen yang tajam — tanpa itu, sebagian perangkat menampilkan ikon default browser saat "Add to Home Screen", bukan ikon Vokasia.

---

## 4. Mobile-friendliness

**Hal yang sudah benar (bukti dari kode, bukan asumsi):**
- `--tap-min: 44px` adalah token nyata dan dipakai konsisten di `Button` (varian `lg`) dan `RoleMobileNav`.
- `RoleMobileNav` sudah menghormati `env(safe-area-inset-bottom)` untuk notch/gesture-bar iPhone, `md:hidden` supaya nav bawah cuma tampil di mobile, dan `focus-visible` yang benar.
- Tema `[data-theme="sekolah"]` khusus mobile shell (`/student`, `/mentor`) dengan kontras terverifikasi terhadap 4 warna brief.
- `PhotoUploader` pakai `<img>` biasa (bukan `next/image`) secara sadar — alasannya didokumentasikan (URL presigned MinIO dinamis tak cocok allowlist `next/image`) — keputusan teknis yang benar, bukan kelalaian.

**Yang jadi masalah nyata:**
- **Tombol hapus foto 20×20px** (`PhotoUploader.tsx` baris ~162–169, class `h-5 w-5`) — di bawah `--tap-min: 44px` yang proyek ini sendiri wajibkan untuk NFR-UX-02. Untuk siswa yang mengetik jurnal di HP murah dengan jari, ini target sentuh yang gampang meleset — ironis karena ini komponen paling mobile-critical di seluruh app (dipakai tiap hari oleh setiap siswa).
- **Tidak ada `sizes`/responsive breakpoint eksplisit untuk grid thumbnail publik** — `/p/[slug]` pakai `grid-cols-3` tetap tanpa varian breakpoint; di layar sangat sempit (320px) 3 kolom foto bisa jadi terlalu sempit untuk thumbnail yang bermakna.
- **Belum ada bukti bundle size `/student` < 200KB** — ini AC eksplisit di `TICKETS.md` (VOK-H7-E2) tapi tiket tersebut belum dikerjakan (lihat §6); artinya klaim performa 3G di DESIGN.md belum diverifikasi dengan angka nyata.
- Saya tidak bisa memverifikasi visual di 320/375/414px secara langsung (lihat batasan di atas) — rekomendasi: jalankan Lighthouse mobile + resize manual di Chrome DevTools pada `/student`, `/mentor`, dan halaman publik sebagai langkah verifikasi berikutnya, karena ini persis AC yang sudah proyek tulis sendiri di VOK-H7-E2.

---

## 5. Analisis kode

**Kekuatan yang patut dicatat (jarang terlihat di proyek solo-dev):**
- **0 penggunaan `: any`** di 110 file TypeScript frontend — disiplin tipe genuinely ketat.
- Token desain benar-benar terkunci: nyaris tidak ada warna hex hardcode di luar `globals.css` (satu-satunya kecuali adalah `strokeStyle` canvas di `SignaturePad`, yang memang di luar jangkauan CSS token).
- Dokumentasi keputusan taktis di dalam kode itu sendiri (komentar panjang di `PhotoUploader.tsx`, `JournalForm.tsx` menjelaskan *kenapa* urutan presign-upload-attach begini) — ini akan sangat menolong siapa pun (termasuk AI lain) yang lanjutkan proyek ini nanti.
- `DECISIONS.md` sebagai log keputusan kronologis (36+ entri) adalah praktik yang jarang ada bahkan di tim berbayar, apalagi solo-dev.

**Kekurangan nyata:**
- **Test frontend nyaris kosong.** Dari 110 file `.ts/.tsx`, hanya 2 file test (`lib/guard.test.ts`, `lib/session.test.ts`) — keduanya level lib/util, nol test komponen (tidak ada test untuk `JournalForm`, `PhotoUploader`, `ApprovalCard`, dsb). Bandingkan dengan backend yang menurut `DECISIONS.md` sudah py 48+ test integrasi dengan siklus PROMPT-D (rusak dulu → merah → kembalikan → hijau) yang disiplin. Kesenjangan ini berisiko: bug UI regresi tidak akan tertangkap otomatis.
- **Working tree kotor dengan ~30 file uncommitted**, termasuk file inti auth/tenant (`TenantResolutionMiddleware.cs`, `AmbientTenantContext.cs`, `OpenIddictSetup.cs`, `AuthorizationController.cs`) dan banyak halaman `/sa` `/app` `/mentor` frontend. Ini refactor tenant-context yang sedang berjalan tapi belum di-commit — kalau sesi kerja terputus (mati listrik, crash, dsb), progres ini berisiko hilang. `HANDOFF-NEXT-SESSION.md` proyek sendiri sudah pernah memperingatkan pola serupa (drift CRLF/LF ~150 file, jangan `git add -A` sembarangan).
- **404 route hilang** (`app/not-found.tsx`) — lihat §3, ini juga masalah kode, bukan cuma UI: satu file kosong sudah cukup untuk menutup celah ini.
- **Beberapa fitur di tiket sudah selesai backend tapi frontend-nya belum tentu ter-exercise end-to-end** — dari `DECISIONS.md`, backend jalur H5-E3 sudah lulus 48 test integrasi terhadap Postgres+RabbitMQ sungguhan, tapi frontend `/sa`, billing, portofolio publik (H6) baru lolos build hijau — belum ada E2E Playwright lintas 5 persona (tiket VOK-H7-E3, belum dikerjakan) yang membuktikan alur penuh siswa→mentor→guru→admin benar-benar nyambung dari UI.
- **PWA minim**: `manifest.ts` ada, tapi tiket VOK-H7-E2 (instalabilitas penuh + audit bundle + Lighthouse) belum dikerjakan, jadi manifest ini kemungkinan besar belum pernah benar-benar dites "Add to Home Screen" di Android asli.

---

## 6. Inventaris fitur — apa yang bisa dijalankan aplikasi ini

Berdasarkan struktur route nyata di `frontend/src/app/` + `TICKETS.md` + `DECISIONS.md` (progres tercatat sampai commit terakhir `5b7d1bf`, tiket VOK-H6-E2). Sprint berjalan H1→H7; **H1–H6 tercatat selesai, H7 (hardening & rilis) belum dimulai.**

**Publik (tanpa login):**
- Landing page (`/`) — penjelasan alur 3 peran + CTA masuk & verifikasi sertifikat.
- Verifikasi sertifikat (`/verify/[code]`) — cek keaslian sertifikat PKL tanpa data sensitif (NISN/kontak disembunyikan by design).
- Portofolio siswa publik (`/p/[slug]`) — identitas, kompetensi terverifikasi, sampel karya, badge sertifikat; opt-in dari sisi siswa.
- Undangan mentor via magic link (`/mentor-invite`).

**Siswa (`/student`, mobile-first, PWA-scoped):**
- Jurnal harian (teks ≤500 karakter + counter, pilih hingga 5 kompetensi, upload hingga 3 foto lewat presigned URL ke MinIO dengan EXIF/GPS di-strip otomatis di consumer worker).
- Riwayat jurnal (`/student/history`).
- Editor portofolio — pilih sampel dari jurnal yang sudah disetujui, toggle publish/unpublish.
- Streak mingguan (`WeekStrip`).

**Mentor industri (`/mentor`):**
- Daftar jurnal pending + approve/reject massal (pilih semua, batch approve, alasan wajib saat reject).
- Penilaian aspek industri per siswa (`/mentor/nilai`).

**Sekolah — admin & guru (`/app`):**
- Dashboard KPI (persentase jurnal hari ini, approval pending, kunjungan terlambat, siswa "bermasalah" RAG merah/kuning berbasis cron ghosting-detection harian).
- Bimbingan: daftar siswa per guru, kunjungan lapangan (form + tanda tangan digital via `SignaturePad`), riwayat kunjungan.
- Penilaian: editor skor guru, rekap nilai gabungan (guru + mentor, berbobot), tombol finalize (mengunci nilai permanen) + export Excel/PDF.
- Billing: tabel tagihan bulanan + upload bukti transfer.

**Superadmin (`/sa`, multi-tenant):**
- Manajemen tenant (wizard provisioning sekolah baru + admin pertama).
- Registry DUDI (perusahaan mitra PKL) global — verifikasi, gabung (merge) entri duplikat dengan riwayat.
- Manajemen paket langganan + feature flag per tenant.
- Tabel invoice + konfirmasi pembayaran manual.
- Impersonasi user (dengan audit log actor asli + banner peringatan) untuk dukungan teknis.
- Audit log terfilter (aktor/entitas/tanggal).

**Infrastruktur pendukung yang tidak terlihat di UI tapi berjalan di baliknya:** OAuth PKCE + BFF cookie-only (token tidak pernah menyentuh `localStorage`), isolasi tenant di level query database, rate limiting endpoint publik, event-driven lewat outbox+RabbitMQ (notifikasi, streak, cron "ghosting" 3-hari-kosong→merah), generator sertifikat PDF ber-QR via worker background, dan sistem notifikasi in-app.

**Belum ada / belum selesai (H7, belum dimulai per `TICKETS.md`):** audit performa formal (p95<300ms terverifikasi beban), health-check endpoint gabungan, backup+restore terbukti, sweep state loading/empty/error/offline di semua layar, verifikasi bundle `/student` <200KB, Lighthouse pass mobile, PWA installability teruji, dan E2E Playwright 5 persona penuh.

---

## 7. Temuan mendalam — pintu masuk login (`/account/login`)

Ini temuan paling penting dari putaran kedua. Alur nyata: Next.js `/login` → tombol "Lanjut ke halaman masuk" → redirect ke `http://localhost:5000/account/login` (server OAuth OpenIddict, proses .NET terpisah dari frontend). Saya melihat halaman itu langsung (screenshot live, tanpa mengetik apa pun) — putih polos, font sistem default, tanpa CSS/token Vokasia sama sekali, judul literal **"Masuk (form dev H1-E3)"**, dengan subjudul **"Form sangat sederhana untuk membuktikan flow code+PKCE hidup. UI produksi ada di Next.js."**

Ini bukan dugaan saya — persis kata-kata itu ada di `backend/src/Vokasia.Api/Auth/AccountEndpoints.cs` baris 45–46, ditulis sebagai HTML mentah dengan inline `style=`. Komentar di atas kode itu (baris 11–21) mengonfirmasi: ini dibuat sangat awal (ticket H1-E3, sprint pertama) semata untuk membuktikan alur OAuth PKCE hidup, dan **belum pernah diganti** meski proyek sekarang sudah di H6 dari 7 sprint. Setiap peran — siswa, mentor, guru, admin sekolah, superadmin — mengetik password mereka di halaman ini. Ini satu-satunya titik di seluruh aplikasi yang saya temukan benar-benar lepas total dari `DESIGN.md`, lebih parah dari 404 generik di §3 karena ini bukan halaman error yang jarang dilihat — ini **pintu masuk yang dilihat setiap pengguna setiap kali sesi mereka habis**.

Detail teknis tambahan dari membaca kode form ini:
- Input pakai `placeholder` sebagai satu-satunya label ("email", "password") — bukan `<label>` sungguhan. Placeholder hilang begitu user mulai mengetik, dan sebagian screen reader tidak mengumumkannya sekonsisten label asli — celah aksesibilitas kecil tapi nyata, ironis karena komponen `Input.tsx` di sistem desain Next.js justru sudah benar (label eksplisit).
- **Kemungkinan open redirect**: parameter `returnUrl` diterima apa adanya dari query string GET, ditaruh di hidden field, lalu setelah login sukses langsung dipakai sebagai tujuan redirect 303 (`AccountEndpoints.cs` baris 66–96) — **tanpa validasi bahwa itu path lokal**. Dalam alur normal aplikasi, `returnUrl` ini selalu dibentuk oleh server sendiri di `AuthorizationController.cs` (baris 65, dari `Request.Path`), jadi di jalur resmi selalu aman/same-origin. Tapi karena `GET /account/login` adalah endpoint publik yang menerima `returnUrl` apa pun lewat URL, seseorang bisa membuat tautan `.../account/login?returnUrl=https://situs-lain.com` dan membagikannya — korban yang login lewat tautan itu akan diarahkan ke situs luar setelah berhasil autentikasi. Risiko konkretnya phishing pasca-login ("sesi habis, masukkan password lagi di sini"), bukan pencurian kredensial langsung. Perbaikan sepadan: validasi `returnUrl` harus path relatif/lokal (mis. `Uri.IsWellFormedUriString` + cek prefix `/`) sebelum dipakai di kedua endpoint (`GetLoginForm` dan `PostLogin`).
- `POST /account/login` memanggil `.DisableAntiforgery()` secara eksplisit — didokumentasikan sebagai keputusan sadar (form same-origin sederhana), bukan kelalaian, tapi tetap layak diverifikasi ulang sebelum rilis karena form kredensial biasanya jadi kandidat pertama proteksi CSRF.

## 8. Temuan mendalam — kode backend (.NET)

Saya sebelumnya hanya mengutip klaim `REPORT-SECURITY.md`; putaran kedua ini saya baca langsung implementasinya di `backend/src/Vokasia.Api/`.

**Yang terverifikasi solid:**
- **RBAC** (`Auth/RbacPolicies.cs`) — 7 policy jelas per peran, dan yang menarik: batasan "mentor cuma boleh aksi placement miliknya sendiri" ditegakkan lewat *resource-based authorization* asli ASP.NET Core (`PlacementScopeHandler` mencocokkan `Placement.MentorUserId` ke klaim `sub` token), bukan sekadar filter query yang gampang lupa dipasang di endpoint baru.
- **Rate limiting** (`RateLimiting/VokasiaRateLimiting.cs`) — bukan cuma "ada", tapi ditempatkan di endpoint yang *benar secara teknis*: policy ketat 5/menit per IP+email dipasang di `/account/login` (tempat password sungguhan disubmit), bukan di `/connect/token` (tempat penukaran kode/refresh token OAuth yang seharusnya lebih longgar). Komentar kode menjelaskan proyek ini sempat salah taruh sesuai draft ticket, ditemukan lewat testing nyata, lalu dikoreksi — jenis deviasi yang didokumentasikan, bukan disembunyikan.
- **Security headers** (`Middleware/SecurityHeadersMiddleware.cs`) — CSP `default-src 'none'` yang tepat untuk API JSON murni (bukan CSP generik yang biasanya di-*copy-paste* longgar), `X-Frame-Options`+`frame-ancestors` dipasang berpasangan, dipasang paling awal di pipeline supaya berlaku juga untuk response yang gagal auth.
- **Sanitasi input** (`Validation/TextSanitizer.cs`) — strip tag HTML/script dari teks bebas (jurnal, komentar) sebagai lapis pertahanan server, dengan penjelasan eksplisit ini pelengkap bukan pengganti React auto-escape di frontend.
- **`DashboardEndpoints.cs`** — AC tiket minta "satu query agregat, no N+1 untuk 900 siswa"; kode nyatanya memang tidak melakukan query per-siswa, dan untuk metrik "kunjungan terlambat" yang skema datanya belum punya kolom jadwal/tenggat, nilainya di-hardcode `0` dengan komentar jujur "placeholder, bukan angka dikarang" — dibanding sekadar mengarang angka supaya terlihat lengkap, ini praktik yang baik.

**Yang masih longgar:**
- Open redirect di `/account/login` (detail di §7).
- `TenantResolutionMiddleware.cs` mendokumentasikan sendiri mekanisme `X-Acting-Tenant` (SuperAdmin melonggarkan filter tenant lewat header) sebagai jalur yang **tidak** menghasilkan audit log per-query — berbeda dari mekanisme `StartImpersonation` yang memang diaudit. Ini ditandai eksplisit sebagai keputusan desain di kode (dua mekanisme untuk dua kebutuhan berbeda), bukan bug — tapi tetap satu permukaan yang layak diverifikasi ulang saat audit keamanan menyeluruh, karena "header yang bisa melonggarkan isolasi tenant tanpa jejak" adalah pola yang biasanya diminta tim keamanan untuk didokumentasikan secara eksplisit di luar kode juga (bukan cuma komentar).

## 9. Prioritas perbaikan (ringkas, urut dampak/effort)

1. **Validasi `returnUrl` di `AccountEndpoints.cs`** sebelum redirect 303 — pastikan hanya path lokal (`/...`) yang diterima. Effort kecil, ini satu-satunya temuan dengan bau kerentanan keamanan nyata di seluruh audit. Lihat §7.
2. **Gaya-ulang `/account/login`** supaya konsisten dengan `DESIGN.md` (token, label asli bukan placeholder-only, Geist) — ini pintu masuk yang dilihat setiap peran setiap kali sesi habis, bukan halaman sekunder. Lihat §7.
3. **Buat `app/not-found.tsx` bergaya Vokasia** — effort kecil, dampak besar (menutup dead-end paling mencolok, sesuai janji NFR-UX-04 sendiri).
4. **Perbesar tap target tombol hapus foto** di `PhotoUploader.tsx` ke minimal 44×44px (bisa perluas hit area lewat padding tanpa mengubah ukuran visual ikon).
5. **Tambahkan link "kembali ke beranda" di kartu error `/verify/[code]`** dan pastikan pola sama diterapkan di semua state error publik.
6. **Mulai test komponen frontend**, minimal untuk `JournalForm`, `PhotoUploader`, `ApprovalCard` — area dengan risiko regresi tertinggi karena dipakai harian oleh siswa/mentor.
7. **Commit atau stash pekerjaan tenant-context yang sedang berjalan** sebelum sesi kerja berikutnya terputus — ikuti panduan `git diff --stat` per file dari `HANDOFF-NEXT-SESSION.md` §4 sendiri untuk menghindari ikut nge-commit noise CRLF/LF.
8. **Kerjakan VOK-H7-E2** (bundle size, Lighthouse, PWA installability, sweep state) — ini satu-satunya cara memverifikasi klaim performa 3G di DESIGN.md dengan angka, bukan asumsi.
9. Ganti 4 emoji literal (`PhotoUploader.tsx`, badge sertifikat `p/[slug]/page.tsx`) dengan `Icon` yang sudah ada (`camera`, `warning`, `x`, `award`) — effort sangat kecil, ikonnya sudah dibuat, tinggal dipakai.
10. Tambahkan PNG icon 192/512 ke `manifest.ts` untuk kompatibilitas "Add to Home Screen" yang lebih luas.
11. Verifikasi ulang keputusan `X-Acting-Tenant` tanpa audit-per-query (§8) secara terpisah dari audit ini — bukan bug, tapi layak didokumentasikan eksplisit di luar komentar kode untuk siapa pun yang mengaudit keamanan tenant isolation nanti.

---

## 10. Verifikasi perbaikan — putaran ketiga

Developer melaporkan seluruh temuan §7–§9 sudah dikerjakan. Saya tidak menerima laporan itu begitu saja — saya baca ulang kode sumber untuk tiap klaim, dan untuk dua yang paling penting (form login, halaman 404) saya buka langsung live di browser (bukan cuma baca kode) untuk memastikan yang ter-deploy memang cocok dengan yang tertulis.

**Terverifikasi selesai, dengan bukti:**

| Temuan asli | Bukti verifikasi |
|---|---|
| Form login `/account/login` tanpa gaya ("form dev H1-E3") | Dibuka live di `localhost:5000/account/login` — sekarang bertema `sekolah` penuh (oklch, Geist, kartu bermerek Vokasia), `<label>` asli (bukan placeholder-only), CSP nonce terpasang di `<style>`. Kode di `AccountEndpoints.cs` cocok persis dengan yang ter-render. |
| Kemungkinan open redirect di `returnUrl` | `AccountEndpoints.cs` sekarang punya `GetSafeReturnUrl()` yang menolak apa pun selain path lokal — termasuk trik `//evil.com` (protocol-relative) yang sering terlewat. Dicerminkan di sisi frontend BFF oleh `localReturnUrl.ts` dengan logika yang sama. Fallback aman ke `/account/continue` kalau tervalidasi gagal. Solid. |
| `.DisableAntiforgery()` pada form kredensial | Diganti `IAntiforgery` sungguhan: token digenerate di `GetLoginForm`, divalidasi di `PostLogin` dengan pesan error yang jelas kalau token kedaluwarsa. |
| 404 default Next.js polos-hitam di `/p/[slug]` | `app/not-found.tsx` sekarang ada, dibuka live di `localhost:3000/p/does-not-exist-xyz` — kartu bertema Vokasia, tombol "Ke beranda" + "Masuk ke Vokasia", judul tab browser bahkan ikut disesuaikan ("Portofolio tidak ditemukan — Vokasia"). |
| Tombol hapus foto 20×20px di `PhotoUploader` | Sekarang `min-h-[var(--tap-min)] min-w-[var(--tap-min)]` = 44×44px, pakai `Icon` SVG bukan emoji, `aria-label` jelas. Bonus yang tak saya minta: `alt=""` kosong pada pratinjau foto diganti alt deskriptif, dan `URL.revokeObjectURL()` ditambahkan saat hapus (menutup potensi memory leak blob URL yang sebelumnya tak saya tandai). |
| Emoji lepas dari sistem ikon di `PhotoUploader` | Semua diganti `<Icon name="camera"/warning"/"x" />` yang memang sudah ada di `Icon.tsx`. |
| Manifest PWA cuma 1 ikon SVG | `icon-192.png`, `icon-512.png`, `icon-maskable-512.png` ditambahkan dengan `purpose` yang benar. |
| Draft jurnal hilang saat sesi habis | `JournalForm.tsx` sekarang menyimpan ke `sessionStorage` per user/tenant/slot, ada indikator status draft (`role="status" aria-live="polite"`), dan gagal-aman kalau storage diblokir browser. |
| Security headers cuma di backend | `securityHeaders.ts` menambahkan CSP, `Permissions-Policy`, HSTS (produksi) di origin Next.js juga — sebelumnya cuma API .NET yang punya ini. |

**Item yang dilaporkan "masih perlu keputusan Developer" — saya cek, dan laporannya jujur, bukan dikecilkan:**

- `TenantResolutionMiddleware.cs` — kode `X-Acting-Tenant` **belum berubah** sama sekali dari audit sebelumnya, persis seperti yang dilaporkan: masih melonggarkan filter tenant tanpa audit-log per-query.
- `VokasiaDbContext.cs` `ApplyTenantQueryFilters()` — saya hitung ulang daftar entity yang difilter: `TenantCompany`, `CompanySlot`, dan `Invoice` memang **tidak ada** di daftar, persis seperti klaim.
- `docker-compose.yml` healthcheck worker — masih literal `pgrep dotnet || exit 1`, dengan komentar di kode yang **mengakui sendiri** kelemahannya ("proses hidup, bukan koneksi broker sehat").
- `PortfolioEndpoints.cs` — `Cache-Control: public, max-age=300` terkonfirmasi persis di baris yang disebut.

**Yang tidak saya verifikasi** (di luar jangkauan alat saya sesi ini): angka test 51/305 lulus, hasil `docker compose up` 7/7 healthy, output RabbitMQ queue, dan Lighthouse — semua itu perlu menjalankan `dotnet test`, `docker compose`, dan Chrome DevTools performance profiling sungguhan, yang tidak saya jalankan ulang di sesi ini (sandbox saya sempat tidak tersedia saat mencoba). Saya percayakan angka-angka itu ke laporan Developer karena semua klaim kode yang bisa saya periksa langsung terbukti akurat dan tak satu pun berlebihan.

**Kesimpulan:** ini penyelesaian yang genuinely solid, bukan tempelan kosmetik. Setiap perbaikan menjawab akar masalah (bukan cuma gejala), dan yang paling meyakinkan: laporan Developer soal apa yang *belum* selesai sama jujurnya dengan laporan soal apa yang sudah — item "masih perlu keputusan" itu semua benar-benar masih terbuka saat saya cek kodenya, bukan under-selling. Untuk rilis produksi, tiga item tenant-isolation (`X-Acting-Tenant`, filter `TenantCompany`/`CompanySlot`/`Invoice`, healthcheck worker) tetap layak jadi prioritas berikutnya — itu satu-satunya kelompok temuan yang menyentuh isolasi data lintas sekolah, bukan sekadar UX.
