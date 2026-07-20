# DECISIONS.md — Decision Log Vokasia

Dipelihara VPM. Setiap keputusan: tanggal, konteks, keputusan, alasan, dampak.

| # | Tgl | Konteks | Keputusan | Alasan | Dampak |
|---|---|---|---|---|---|
| D1 | 2026-07-20 | Nomor bab di instruksi VPM ≠ struktur PRD aktual | `[ASSUMPTION]` mapping: "Bab 15"→Bagian 3, "Bab 5 MoSCoW"→notasi M/S/C Bagian 2, "Bab 6"→matrix 2.3, "Bab 7.3"→2.4, "Bab 13"→NFR-SEC-05 | PRD terkonsolidasi jadi 6 bagian | Nol, kecuali Dev koreksi |
| D2 | 2026-07-20 | Pemilihan IDE (brainstorm tooling) | ~~JetBrains Rider~~ → **direvisi hari yang sama: VS Code + C# Dev Kit** (keputusan Dev) | Gratis; satu jendela backend+frontend+Docker; cukup untuk peran merge/review | Tanpa biaya lisensi; hapus item trial Rider |
| D3 | 2026-07-20 | Cara kerja ENG (ChatGPT) di repo | **Hybrid**: diskusi task di chat, implementasi via Codex di branch `h{N}-eng{X}-{slug}` | Kecepatan + kontrol via review diff; hemat waktu Dev (bottleneck 4–6 jam/hari) | Butuh git init + AGENTS.md (done) |
| D4 | 2026-07-20 | Struktur memory files | Set lengkap: `AGENTS.md` + `SOUL.md` + `DESIGN.md` + `DECISIONS.md` + `TOOLING.md`; **SOUL.md menggantikan persona-*.md** | Satu sumber identitas per file, tanpa duplikasi persona | PRD Lampiran ref persona-*.md obsolete |
| D5 | 2026-07-20 | MCP tambahan | Context7 (pra-sprint) + PostgreSQL MCP (H1) + Playwright MCP (H6) — **sequencing, bukan sekaligus** | Kurangi API halu; inspeksi DB saat review; E2E H7. Sequencing = anti tool-sprawl | Setup bertahap, tidak makan pra-sprint |
| D6 | 2026-07-20 | Pemakaian hallmark | Dijalankan **sekali pra-H1** → isi `DESIGN.md` tokens+voice → beku | Kontrak visual anti-slop untuk ENG-2; sesuai design-in-code PRD 4.1 | Dilarang dipakai mempercantik landing saat sprint |
| D7 | 2026-07-20 | Figma MCP terpasang di Claude Desktop | Ditunda pasca-MVP | PRD 4.1 eksplisit tanpa fase Figma MVP | Nol |
| D8 | 2026-07-20 | Temuan: `D:\Web\Vokasia` **belum git init** padahal scaffold sudah ada | Wajib `git init` + push GitHub private **sebelum sprint** | Mitigasi R5 (rollback) & R6 (commit harian); prasyarat workflow Codex | Langkah 1 di TOOLING.md §3 |
| D9 | 2026-07-20 | codebase-memory-mcp terinstall via npm tapi belum tersambung ke sesi VPM | Jalankan `codebase-memory-mcp install` + restart Claude Desktop & Codex; index perdana H1 | Repo masih kecil — index bermakna mulai H1 | VPM dapat impact analysis saat review |

## Backlog Minggu 2+

(kosong — belum ada ide yang ditolak dari scope sprint)
