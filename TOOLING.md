# TOOLING.md — Master Tooling Vokasia

Status per 20 Jul 2026 (pra-sprint). Keputusan tercatat di `DECISIONS.md`. Perubahan tooling saat sprint = change control (PRD 3.6).

## 1. Set wajib

| # | Tool | Fungsi | Pemakai | Kapan setup | Status |
|---|---|---|---|---|---|
| 1 | .NET 10 SDK · Bun ≥1.2 · Docker Desktop | Runtime (PRD 0.2) | Dev | Sudah | ✅ (scaffold jalan) |
| 2 | **git + GitHub private repo** | Versioning, rollback (R5), commit harian (R6), prasyarat workflow Codex | Semua | **SEKARANG — repo belum `git init`** | ⬜ |
| 3 | **VS Code + C# Dev Kit** | IDE utama: merge, debug, run — satu jendela untuk backend + frontend + Docker + git. Gratis | Dev | Pra-sprint | ⬜ |
| 4 | **codebase-memory-mcp** | Knowledge graph repo: impact analysis & call-chain saat review; konteks struktur untuk ENG | VPM + ENG | Connect pra-sprint; index perdana H1; re-index tiap merge | ✅ installed · ⬜ connect |
| 5 | **hallmark** (design skill) | Generate `DESIGN.md` + tokens — kontrak visual anti-slop | Dev (sekali) | Pra-H1 | ⬜ |
| 6 | **Codex (ChatGPT)** | ENG-1/2/3 implementasi langsung di branch repo, baca `AGENTS.md` | ENG | Pra-sprint | ⬜ |
| 7 | Desktop Commander + Windows-MCP | VPM eksekusi `dotnet build/test`, `bun test`, git di mesin Dev saat review — verifikasi klaim test | VPM | — | ✅ tersambung |

## 2. MCP ekstra (sequencing — JANGAN pasang semua sekarang)

| Tool | Fungsi | Kapan pasang |
|---|---|---|
| Context7 | Docs real-time .NET 10 / Next.js 16 / MassTransit → kurangi API halu ENG | Pra-sprint (Codex ENG + Claude Desktop) |
| PostgreSQL MCP | VPM inspeksi skema/data saat review migration (R12) | H1, setelah `docker compose up postgres` |
| Playwright MCP | E2E 5 persona (ENG-3) | H6, dipakai H7 |

## 3. Langkah setup pra-sprint (urutan, PowerShell)

```powershell
# 1) Git — hari ini
cd D:\Web\Vokasia
git init -b main
git add -A; git commit -m "chore: scaffold + project docs"
# buat repo private GitHub → git remote add origin <url> → git push -u origin main

# 2) codebase-memory-mcp — auto-configure semua agent terdeteksi
codebase-memory-mcp install
# lalu restart Claude Desktop & Codex; di agent ucapkan "Index this project"

# 3) hallmark — install skill, jalankan sekali untuk DESIGN.md
npx skills add nutlope/hallmark
# jalankan via Codex/Claude Code di repo: brief = app PKL (bukan landing),
# audience siswa SMK HP murah, tone sederhana-fungsional, patuhi PRD 4.1 + W1–W6
# → setelah puas: "lock the system" → design.md → merge ke DESIGN.md

# 4) Context7 — tambah ke config Codex + Claude Desktop (lihat docs context7)

# 5) VS Code — install extensions:
#    C# Dev Kit · Docker · ESLint · Tailwind CSS IntelliSense · EditorConfig
#    buka D:\Web\Vokasia sebagai workspace (backend\Vokasia.sln terdeteksi C# Dev Kit)
```

## 4. Pembagian pemakaian

- **Dev**: VS Code (merge/debug) · git (final approver merge) · hallmark (sekali).
- **VPM**: Desktop Commander/Windows-MCP (jalankan build+test saat review) · codebase-memory (impact analysis) · PostgreSQL MCP (H1+).
- **ENG via Codex**: codebase-memory (paham struktur) · Context7 (docs) · `AGENTS.md` + `SOUL.md` (aturan) · `DESIGN.md` (ENG-2).

## 5. Ditunda / tidak dipakai MVP

- **Figma MCP** — terpasang tapi PRD 4.1 eksplisit tanpa fase Figma MVP → pasca-MVP.
- Midtrans/WA/belajar.id tooling — out-of-scope (PRD 1.3).
