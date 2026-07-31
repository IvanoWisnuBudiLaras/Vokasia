# Audit Vokasia — Indeks

Rangkaian audit kesiapan produksi Vokasia, kronologis. Tiap putaran memverifikasi fix putaran sebelumnya lalu berburu temuan baru; cakupan menyempit tiap kali (tanda hardening yang sehat).

| # | Berkas | Fokus | Status tindak lanjut |
|---|---|---|---|
| v1 | [AUDIT-HALLMARK-UX-UI-KODE.md](./AUDIT-HALLMARK-UX-UI-KODE.md) | UX/UI, mobile-friendliness, analisis kode, inventaris fitur; login OAuth mentah, open-redirect, 404 generik, tap-target | ✅ ditindaklanjuti |
| v2 | [AUDIT-VOKASIA-V2.md](./AUDIT-VOKASIA-V2.md) | Baseline pasca-perbaikan; copy, password toggle, kontras, duplikasi validasi returnUrl | ✅ ditindaklanjuti |
| v3 | [AUDIT-VOKASIA-V3.md](./AUDIT-VOKASIA-V3.md) | Pengujian runtime + serangan langsung: password-spraying, env Dev di prod, ForwardedHeaders, IDOR object-key, scope guru, temuan UI terautentikasi (nav peran, sticky, notifikasi, billing merah), shell mentor/siswa mobile-only | ✅ ditindaklanjuti |
| v4 | [AUDIT-VOKASIA-V4.md](./AUDIT-VOKASIA-V4.md) | Pengujian total: IDOR storage (3 endpoint+worker+portfolio), scope guru API, kuota TOCTOU, bobot rubrik, finalize concurrency | ✅ ditindaklanjuti |
| v5 | [AUDIT-VOKASIA-V5.md](./AUDIT-VOKASIA-V5.md) | Verifikasi fix v4 + gap baru: pencabutan sesi saat deactivate (V5-1) | ✅ ditindaklanjuti |

## Residual terbuka (di luar v1–v5, ditemukan saat verifikasi lanjutan)

Belum dibungkus sebagai audit bernomor — dicatat di sini agar tak hilang:

- **TLS edge untuk topologi prod** — profil prod meletakkan port di loopback dan mengandalkan reverse-proxy TLS; login browser putus tanpa proxy. Fix ditulis (belum diuji auditor): [`../../deploy/edge/`](../../deploy/edge/) (`Caddyfile` + service `caddy` profile `edge` + runbook). Perlu dijalankan & diverifikasi.
- **Bootstrap SuperAdmin** — tak ada jalur resmi membuat operator platform pertama (`seed demo` diblokir di prod & tak membuat SuperAdmin). Rekomendasi: CLI `seed superadmin` ter-gated (email+password dari env, idempoten). Belum diimplementasikan.
- **Readiness-check SMTP** — email diam-diam tak terkirim bila `Smtp__Host` kosong; disarankan startup-warning di Production. Belum diimplementasikan.
- **Verifikasi yang belum dilakukan** (jangan dianggap lulus): E2E terautentikasi 5-persona di 320/375/414/768, Lighthouse/PWA di perangkat nyata, uji konkurensi beban paralel live, `dotnet test`/`bun test` penuh di CI Linux.
- **Tindak lanjut Qodana** (`baseline.sarif.json`, 1091 temuan) — rencana kerja terprioritas untuk AI: [`../plans/qodana-cleanup-plan.md`](../plans/qodana-cleanup-plan.md). Ringkas: 1 error (advisory `Microsoft.OpenApi`, prod-unreachable), sisanya hygiene; 255 "NotAccessedPositionalProperty" di `Dtos.cs` adalah **false-positive serialisasi — jangan dihapus**.

## Catatan

- Dokumen kanonik proyek (`PRD.md`, `DESIGN.md`, `DECISIONS.md`, `AGENTS.md`, dll.) sengaja **tetap di root** — dibaca dari sana oleh kode/skill/konvensi; jangan dipindah ke sini.
- Batas metodologi auditor yang berlaku di semua putaran: login tak bisa dilakukan auditor (classifier), Docker tak tersedia di sandbox auditor; verifikasi bersumber dari kode + query DB + probe HTTP + uji serangan, plus render publik lewat browser.
