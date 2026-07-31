# Rencana Kerja: Tindak Lanjut Qodana `baseline.sarif.json`

**Untuk: agen AI pengeksekusi (Codex/Claude Code).** Dokumen ini adalah rencana kerja yang dapat langsung dijalankan. Ikuti urutan prioritas, hormati **titik bahaya**, dan jangan lewati **gerbang verifikasi**.

## 0. Konteks & prinsip (baca dulu — menentukan seluruh sikap kerja)

Scan: **JetBrains Qodana (.NET)**, 1091 temuan, 408 rule. Sebaran severity:

- **error: 1** — satu-satunya, dan itu advisory dependency yang **sudah dinilai** (lihat P0).
- **warning: 432**, **note: 658** — mayoritas **kebersihan/idiomatik kode**, bukan bug fungsional atau lubang keamanan.

**Sikap wajib:** ini **pass hygiene**, BUKAN remediasi keamanan. Jangan over-invest, dan **jangan pernah mengubah perilaku runtime atau kontrak API demi memuaskan linter.** Banyak temuan adalah **false-positive** karena analisis statis tak melihat pemakaian lewat serialisasi/refleksi/DI (lihat P3). Bila ragu antara "menyenangkan linter" vs "menjaga kontrak/perilaku", **selalu pilih menjaga kontrak** dan suppress temuannya di baseline.

## Gerbang verifikasi (jalankan setelah SETIAP batch, tanpa kecuali)

```bash
dotnet build backend/Vokasia.slnx -c Release        # harus exit 0
dotnet test  backend/tests/Vokasia.Tests/Vokasia.Tests.csproj --no-restore   # harus 0 gagal
```
Test suite **sudah bisa hijau lokal** sejak blocker Testcontainers dinormalisasi (`DockerEndpointNormalizer`, harness-only). Kalau setelah suatu batch ada test yang gagal → **batch itu mengubah perilaku; revert, jangan longgarkan test.** Juga review `git diff` tiap batch dan pastikan tak ada perubahan di luar niat. Commit per batch dengan pesan jelas (`chore(qodana): ...`).

---

## P0 — Satu-satunya `error`: advisory `Microsoft.OpenApi 2.1.0` (RiderSecurityErrorsInspection)

**Fakta:** ini duplikat advisory yang sudah dibahas di audit. `Microsoft.OpenApi` **hanya** dipakai `app.MapOpenApi()` di `Program.cs`, yang **di-gate `IsDevelopment()`** → **tidak pernah dijalankan di produksi** → paparan prod ≈ nol.

**Tugas (pilih satu, dokumentasikan):**
1. **Bump** ke versi patched bila ada rilis fixed yang kompatibel (`dotnet list backend/Vokasia.slnx package --vulnerable --include-transitive` dengan akses jaringan, lalu naikkan pin di `Vokasia.Api.csproj`), **atau**
2. **Accept-with-justification**: tambah entri `DECISIONS.md` mencatat "tak terjangkau prod (MapOpenApi Dev-only), diterima sampai upstream rilis fix" dan suppress di baseline Qodana.

**Titik bahaya:** jangan menaikkan versi lintas-major buta lalu menganggap beres — verifikasi build+test hijau. Jangan hapus `MapOpenApi` (berguna di dev).

**Selesai bila:** advisory hilang dari scan ATAU tercatat resmi di `DECISIONS.md` + baseline.

---

## P1 — Warning analyzer Microsoft `CA*` (~80 temuan) — nyata, kecil, aman

Idiom performa/kebenaran sejati. Kerjakan **mekanis** tapi verifikasi tiap batch.

| Rule | Arti | Aksi |
|---|---|---|
| **CA1873** (48) | Argumen log mahal dievaluasi walau level nonaktif | Bungkus dengan `if (logger.IsEnabled(LogLevel.X))` atau pakai source-generated logging |
| **CA1861** (17) | Array konstan sbagai argumen dialokasi berulang | Angkat jadi `static readonly` field |
| **CA1822** (8) | Member tak menyentuh state instance | Jadikan `static` |
| CA1068 / CA1827 / CA1859 (sisanya) | CancellationToken bukan param terakhir / `Any()` bukan `Count()>0` / tipe konkret utk perf | Ikuti saran analyzer satu per satu |

**Titik bahaya:** CA1068 (urutan `CancellationToken`) mengubah **tanda tangan method** — pastikan semua pemanggil ikut diperbarui (build akan menangkap). CA1822 → `static`: jangan bila method itu di-override atau bagian interface.

**Selesai bila:** seluruh CA* hilang, build+test hijau.

---

## P2 — Kebersihan gaya auto-fixable (~490 temuan) — SATU batch alat, bukan manual

`ArrangeTrailingCommaInMultilineLists`(160), `PropertyCanBeMadeInitOnly`(139), `RedundantUsingDirective`(45), `ConvertToPrimaryConstructor`(45), `RedundantNameQualifier`(36), `RedundantSuppressNullableWarningExpression`(18), `RedundantAnonymousTypePropertyName`(16), `UseCollectionExpression`(12), `RedundantArgumentDefaultValue`(8), `RedundantTypeDeclarationBody`(5), `InvertIf`(6).

**Aksi:** jalankan pemformat/cleanup otomatis dalam **satu commit terpisah** agar diff-nya jelas & mudah di-review/revert:
```bash
dotnet format backend/Vokasia.slnx            # whitespace/using/trailing-comma
# (opsional, lebih lengkap) JetBrains cleanupcode CLI dengan profil yang cocok
```
Lalu **gerbang verifikasi**. Commit: `style(qodana): apply automated formatting & redundancy cleanup`.

**Titik bahaya:**
- **`ConvertToPrimaryConstructor` (45)** BUKAN murni kosmetik — mengubah bentuk kelas; bisa berinteraksi dengan DI, urutan inisialisasi field, atau atribut. **Jangan** paksakan otomatis pada kelas yang punya logika di konstruktor; lakukan hanya yang benar-benar trivial, dan verifikasi test. Kalau ragu, **skip**.
- **`PropertyCanBeMadeInitOnly` (139)** aman untuk model/DTO internal, tapi **JANGAN** pada properti yang di-set setelah konstruksi oleh EF Core, deserializer, atau kode yang me-mutate (mis. status entity). Build+test akan menangkap yang salah.
- `RedundantUsingDirective`: aman, tapi hati-hati using yang hanya dipakai di blok `#if`/analyzer.

**Selesai bila:** kategori ini nol/berkurang drastis, build+test hijau, diff hanya gaya.

---

## P3 — "Unused / Not-accessed" (~387 temuan) — ⚠️ ZONA JEBAKAN, WAJIB JUDGMENT

`NotAccessedPositionalProperty.Global`(243, **255 di `Dtos.cs`**), `ClassNeverInstantiated.Global`(44), `UnusedMethodReturnValue.Global`(27), `MemberCanBePrivate.Global`(21), `UnusedType.Global`(13), `UnusedMember.Global`(8), `UnusedVariable`(8), `MemberCanBeMadeStatic.Local`(8), `NotAccessedPositionalProperty.Local`(15).

**INI BUKAN "kode mati" — mayoritas false-positive.** Qodana tak melihat pemakaian lewat serialisasi/refleksi/DI. **Menghapusnya akan MERUSAK runtime/kontrak API.** Aturan keras:

- **`Dtos.cs` (255) & `OutboxEventContracts.cs` (10) & event/enum records** → properti positional record ini di-**serialisasi ke JSON** (respons API) & di-**bind dari request**. **JANGAN HAPUS satu pun.** Menghapus akan mengubah/merusak payload API yang dikonsumsi frontend. → **Suppress di baseline**, jangan sentuh kode.
- **`ClassNeverInstantiated.Global` (44)** — banyak adalah **entity EF Core, consumer MassTransit, validator FluentValidation, handler** yang diinstansiasi via **refleksi/DI**, bukan `new`. **JANGAN hapus** tanpa membuktikan tak dipakai DI/EF/refleksi. → default: suppress.
- **`UnusedType/UnusedMember/UnusedMethodReturnValue`** — periksa **satu per satu**. Hapus **hanya** bila benar-benar internal, tak dipakai, tak ter-serialisasi, tak ter-refleksi. Bila ragu → suppress, jangan hapus.
- **Yang AMAN dibersihkan:** `UnusedVariable` (8, lokal murni), `MemberCanBeMadeStatic.Local` (8), `MemberCanBePrivate.Global` (21 — mempersempit akses aman selama bukan bagian kontrak publik/serialisasi).

**Proses wajib untuk kategori ini:** JANGAN bulk-delete. Untuk tiap kandidat hapus, cari dulu pemakaian tersembunyi:
```bash
grep -rn "NamaTipe\|NamaProperti" backend/src backend/tests --include=*.cs   # cek refleksi/serialisasi/DI
```
Lalu putuskan **hapus** (terbukti mati) atau **suppress baseline** (framework-bound). Pisahkan jadi commit `chore(qodana): remove verified-dead code` vs update baseline.

**Selesai bila:** kode mati sejati terhapus (build+test hijau, kontrak API tak berubah — verifikasi respons endpoint tak berubah), sisanya di-baseline dengan alasan.

---

## P4 — Note sisa (opsional, polish)

`PreferConcreteValueOverDefault`(44), `InvalidXmlDocComment`(15), dan note lain. Prioritas rendah. `InvalidXmlDocComment` layak diperbaiki (dokumentasi benar); sisanya opsional. Jangan menghabiskan waktu di sini sebelum P0–P3 tuntas.

---

## Kelola baseline Qodana (agar scan berikutnya bersih & tak regresif)

Temuan yang **sengaja diterima** (false-positive serialisasi, advisory prod-unreachable, gaya yang tak diadopsi) jangan dibiarkan muncul terus. Jadikan **baseline resmi**:
```bash
# Qodana memakai file baseline agar temuan lama tak diulang; temuan BARU tetap muncul.
qodana scan --baseline qodana.sarif.baseline   # atau via qodana.yaml: "baseline: <file>"
```
Commit baseline. Setelah itu, **temuan baru di PR mendatang = sinyal nyata**, bukan tenggelam di 1000 note lama.

---

## Ringkasan urutan eksekusi untuk AI

1. **P0** — putuskan OpenApi advisory (bump / accept+DECISIONS), 1 item.
2. **P1** — `CA*` (~80), mekanis + verifikasi. Commit.
3. **P2** — `dotnet format` batch (~490), verifikasi, diff review. Commit. (Skip `ConvertToPrimaryConstructor` yang non-trivial.)
4. **P3** — bersihkan **hanya kode mati terbukti** (variable/local/private-narrowing); **suppress** DTO/entity/consumer/event ke baseline. **JANGAN hapus properti DTO.** Commit terpisah.
5. **P4** — opsional (`InvalidXmlDocComment`).
6. **Baseline** — jadikan sisa yang diterima sebagai baseline resmi; commit.

**Gerbang di tiap langkah:** `dotnet build` exit 0 **dan** `dotnet test` 0 gagal **dan** `git diff` hanya perubahan yang diniatkan. Kalau salah satu gagal → revert batch itu.

**Definition of done keseluruhan:** scan ulang Qodana → 0 error; warning/note tersisa hanya yang sudah di-baseline dengan alasan tertulis; build+test hijau; **tak ada perubahan kontrak API atau perilaku runtime** (bukti: respons endpoint & test integrasi tak berubah).
