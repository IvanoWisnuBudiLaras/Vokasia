# template.md — Kumpulan Prompt & Aturan Eksekusi Ticket

## Arsitektur eksekusi (siapa mengerjakan apa)

| Peran | Pelaku | Tugas |
|---|---|---|
| **Coder** | **ChatGPT** (ENG-1/2/3, model per ticket: GPT-5.4 Thinking / GPT-5.3-Codex / GPT-5.6 Luna) | Mendesain & menulis kode + test + walkthrough, berdasarkan file ticket |
| **Runner** | **Claude Sonnet 5** + Windows-MCP di `D:\Web\Vokasia` | Buat branch, terapkan kode ChatGPT ke file, jalankan build/test via PowerShell, commit, lempar balik error ke ChatGPT, setor hasil ke VPM |
| **Reviewer** | VPM (Claude) | Verdict APPROVE / REQUEST CHANGES |
| **Merger** | Developer (manusia) | Merge ke `main`, keputusan final |

**Batas runner**: runner TIDAK mendesain/menulis logika. Boleh fix mekanis ≤5 baris (path salah, using/import hilang, typo nama) dan WAJIB mencatatnya di laporan. Error logika → kembalikan ke ChatGPT (Prompt B). Dilarang: merge/push `main`, ubah file di luar wilayah ticket, tambah dependency, ubah kontrak OpenAPI/skema beku, melonggarkan test.

## Skala Effort (ketelitian RUNNER, tertera per ticket)

| Effort | Kapan | Arti operasional untuk runner |
|---|---|---|
| **light** | Pola jelas, risiko rendah | Terapkan → 1× build + test terkait → setor. |
| **medium** | Task standar | Terapkan per kelompok file → unit test terkait → 1× full build → setor. |
| **high** | Banyak fungsi/permukaan | Terapkan bertahap per fitur, test per tahap. Cek `git diff --stat` vs wilayah ticket sebelum setor. |
| **extra high** | Async/kalkulasi rawan silent bug | Semua level high + jalankan suite 2× (kedua kalinya dari state bersih: `docker compose restart` service terkait) + cek log worker/queue saat test. |
| **max** | Security-critical | Semua level extra high + audit hasil ChatGPT terhadap tiap butir `AGENTS.md §non-negotiable` + jalankan negative test (lintas tenant, role salah, token reuse). Ada butir gagal → Prompt B ke ChatGPT, bukan diperbaiki sendiri. |
| **ultra** | Gate rilis / lintas sistem | Semua level max + clean state penuh (`docker compose down -v` → `up -d` → seed → suite lengkap) + laporan bukti per butir AC. |

## Alur per ticket (ritual)

1. Paste **PROMPT A** (+ isi file ticket) ke chat ChatGPT ENG ybs, model sesuai header ticket.
2. ChatGPT balas: TASK LIST → kode per file (path lengkap) → test → WALKTHROUGH → [ASSUMPTION].
3. **PROMPT R** ke Sonnet 5 runner: terapkan + verifikasi sesuai effort ticket.
4. Merah → runner kirim error verbatim ke ChatGPT via **PROMPT B** → ulangi 2–3. Maks 3 putaran; masih merah → eskalasi Developer.
5. Hijau → setor ke VPM (kontrak output lengkap) → verdict → fix (PROMPT B) → Developer merge.

## KONTRAK OUTPUT ChatGPT (wajib, urut — tanpa ini otomatis REQUEST CHANGES)

1. **TASK LIST** — checklist subtask, SEBELUM kode.
2. **KODE + TEST** — per file dengan path lengkap dari root repo; test menyertai (unit utk logic, integration utk endpoint kritis).
3. **WALKTHROUGH** — per file: apa, kenapa, AC mana yang dipenuhi.
4. **[ASSUMPTION]** — daftar asumsi, atau "tidak ada".
(Output test dilampirkan oleh runner, bukan diklaim ChatGPT.)

---

## PROMPT A — ke ChatGPT (coder)

```
Kamu adalah {ENG-x} proyek Vokasia. Model effort-mu penuh untuk ticket ini.
Terlampir: SOUL.md (role-mu), AGENTS.md (aturan teknis WAJIB), file ticket
{VOK-Hx-Ex}.md{, DESIGN.md untuk ticket E2}, potongan PRD yang dirujuk ticket.
Tulis implementasi lengkap ticket tersebut.
Aturan output — WAJIB urut: (1) TASK LIST sebelum kode; (2) KODE + TEST per file
dengan path lengkap dari root repo (kode utuh per file, bukan potongan); 
(3) WALKTHROUGH per file: apa, kenapa, AC mana; (4) [ASSUMPTION] atau "tidak ada".
Catatan: kamu TIDAK mengeksekusi. Kode akan diterapkan & dites oleh runner di
mesin lokal; error akan dikembalikan padamu. Jangan menulis "jalankan X dulu" —
tulis kode final. Ide di luar ticket → bagian "Catatan", jangan implement.
Dilarang: dependency baru, ubah kontrak OpenAPI/skema, sentuh wilayah ENG lain.
```

## PROMPT R — ke Sonnet 5 runner (Windows-MCP)

```
Kamu adalah RUNNER proyek Vokasia (bukan coder). Baca:
D:\Web\Vokasia\ticket\template.md (peran & batas runner, skala effort),
D:\Web\Vokasia\ticket\{VOK-Hx-Ex}.md, D:\Web\Vokasia\AGENTS.md.
Effort ticket ini: {level}.
Di bawah ini output ChatGPT untuk ticket tsb: {paste output ChatGPT}.
Kerjakan: (1) git checkout -b {branch} dari main yang bersih; (2) terapkan kode
per file persis seperti ditulis; (3) jalankan verifikasi sesuai effort {level};
(4) commit kecil per kelompok file, pesan imperative English.
Lapor: file diterapkan · fix mekanis yang kamu lakukan (jika ada, maks ≤5 baris,
sebutkan) · OUTPUT TEST VERBATIM · status HIJAU/MERAH per suite · git log branch.
Jika MERAH: JANGAN perbaiki logika sendiri — siapkan paket error (error verbatim
+ file + konteks) untuk dikembalikan ke ChatGPT.
```

## PROMPT B — ke ChatGPT (perbaikan: error runner ATAU temuan review VPM)

```
Ticket {VOK-Hx-Ex}, branch {branch}. Hasil eksekusi/review:
{paste error verbatim runner ATAU verdict VPM Blocker/Critical/Major/Minor}
Perbaiki SEMUA (Blocker & Critical wajib). Jangan menambah scope.
Output: hanya file yang berubah (kode utuh per file + path), alasan per perbaikan,
dan test baru/berubah bila perlu. Format kontrak output tetap berlaku.
```

## PROMPT C — resume sesi terputus (ke ChatGPT atau runner)

```
Lanjutkan ticket {VOK-Hx-Ex} branch {branch}. Jangan mulai dari nol.
Runner: jalankan git log --oneline -10 + git status, baca TASK LIST terakhir,
tentukan subtask tersisa, lanjutkan. ChatGPT: ini state terakhir {paste ringkasan
runner}, lanjutkan dari subtask yang belum selesai.
```

## PROMPT D — self-check pra-setor (wajib effort max/ultra, dijalankan runner)

```
Sebelum setor ticket {VOK-Hx-Ex} ke VPM: audit sebagai reviewer skeptis.
Periksa & laporkan LOLOS/GAGAL + bukti per butir: (1) semua AC ticket;
(2) tiap butir AGENTS.md §non-negotiable yang relevan; (3) test menguji perilaku
nyata (ubah 1 logika inti → test harus merah — kembalikan lagi); (4) git diff
main --stat hanya menyentuh wilayah ticket. GAGAL → paket error ke ChatGPT dulu.
```

---

## Matrix: ticket × coder × effort runner

| Hari | Ticket | Wilayah | Coder (ChatGPT) | Effort runner |
|---|---|---|---|---|
| H1 | VOK-H1-E1 compose+migrations | ENG-1 | GPT-5.3-Codex | high |
| H1 | VOK-H1-E2 tokens+shells | ENG-2 | GPT-5.3-Codex | medium |
| H1 | VOK-H1-E3 OpenIddict+threat model | ENG-3 | GPT-5.4 Thinking | max |
| H2 | VOK-H2-E1 seeder+endpoint inti | ENG-1 | GPT-5.3-Codex | high |
| H2 | VOK-H2-E2 login UI+guards | ENG-2 | GPT-5.6 Luna | light |
| H2 | VOK-H2-E3 BFF+RBAC+magic link | ENG-3 | GPT-5.4 Thinking | **ultra** |
| H3 | VOK-H3-E1 journal API+cron | ENG-1 | GPT-5.3-Codex | high |
| H3 | VOK-H3-E2 UI jurnal+approve | ENG-2 | GPT-5.3-Codex | high |
| H3 | VOK-H3-E3 immutability+validasi | ENG-3 | GPT-5.4 Thinking | max |
| H4 | VOK-H4-E1 outbox+consumers | ENG-1 | GPT-5.4 Thinking | extra high |
| H4 | VOK-H4-E2 dashboard RAG | ENG-2 | GPT-5.3-Codex | medium |
| H4 | VOK-H4-E3 DLQ tests+email | ENG-3 | GPT-5.4 Thinking | extra high |
| H5 | VOK-H5-E1 assessment+sertifikat | ENG-1 | GPT-5.4 Thinking | extra high |
| H5 | VOK-H5-E2 UI nilai+kunjungan | ENG-2 | GPT-5.3-Codex | medium |
| H5 | VOK-H5-E3 integration tests | ENG-3 | GPT-5.4 Thinking | max |
| H6 | VOK-H6-E1 /sa+billing+portfolio | ENG-1 | GPT-5.3-Codex | high |
| H6 | VOK-H6-E2 UI SA+portofolio publik | ENG-2 | GPT-5.3-Codex | medium |
| H6 | VOK-H6-E3 impersonation+hardening | ENG-3 | GPT-5.4 Thinking | max |
| H7 | VOK-H7-E1 perf+ops+README | ENG-1 | GPT-5.3-Codex | high |
| H7 | VOK-H7-E2 states+low-data | ENG-2 | GPT-5.6 Luna | medium |
| H7 | VOK-H7-E3 E2E+security report | ENG-3 | GPT-5.4 Thinking | **ultra** |

> Nama model = lineup ChatGPT Jul 2026; cek picker akunmu. Konvensi C#: semua endpoint/service async + `CancellationToken ct` — tidak diulang di signature ticket.
