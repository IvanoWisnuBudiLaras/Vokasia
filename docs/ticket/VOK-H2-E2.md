# VOK-H2-E2 — Login UI + route guards + dashboard shell berisi seed

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-2 `frontend/` | `h2-eng2-login-guards` | GPT-5.6 Luna | light | **M1** (login 4 role) | PRD FR-AUTH-01/05, §4.2, DESIGN.md |

## Tugas

UI login (delegasi ke BFF flow milik H2-E3), route guard per segment × role di `proxy.ts`, dan dashboard shell tiap role menampilkan data seed — bukti wiring end-to-end M1. Kontrak dengan H2-E3: BFF menyediakan `GET /api/auth/session → {user:{id,name,role,tenantId}}` dan `POST /api/auth/login|logout`.

## Implementasi

- `app/login/page.tsx` — tujuan: halaman masuk (tombol → redirect BFF `/api/auth/login`); state error dari query (`?error=`); copy sederhana.
- `proxy.ts` (root frontend) — `middleware(req: NextRequest) → NextResponse` — tujuan: guard matrix segment×role: `/sa`→SuperAdmin, `/app`→TenantAdmin|DeptHead|Teacher, `/mentor`→IndustryMentor, `/student`→Student; tanpa session → redirect `/login`; role salah → redirect home role-nya; `/p/*` `/verify/*` publik. Sumber role: cookie session via `getSessionEdge(req)`.
- `lib/session.ts` — `getSession() → Session|null` (server), `getSessionEdge(req) → SessionLite|null` (middleware, tanpa panggil DB) — tujuan: satu sumber pembacaan session di FE; **tidak pernah menyentuh token** (hanya cookie httpOnly, dibaca BFF).
- `lib/roleHome.ts` — `roleHome(role) → '/sa'|'/app'|'/mentor'|'/student'` — tujuan: pemetaan tujuan redirect tunggal.
- Dashboard shell berisi seed (pakai `fetcher` + komponen inti):
  - `(student)/student/page.tsx` — tujuan: sapaan nama + placement (perusahaan) dari seed; kerangka W1 (slot jurnal = placeholder H3).
  - `(mentor)/mentor/page.tsx` — tujuan: daftar siswa bimbingan seed (jumlah pending = placeholder H3).
  - `(school)/app/page.tsx` — tujuan: kartu ringkas (jml siswa, placement aktif) dari endpoint H2-E1.
  - `(sa)/sa/page.tsx` — tujuan: daftar tenant seed ringkas.
- `components/LogoutButton.tsx` — tujuan: POST `/api/auth/logout` → redirect `/login`.

## Acceptance Criteria

- Given tiap kombinasi 4 role × 5 segment, When akses, Then hanya segment sah yang lolos (uji matrix — boleh test middleware unit).
- Given login sukses role X, Then mendarat di home role X dengan data seed tampil.
- Given inspeksi devtools, Then **tidak ada token** di localStorage/sessionStorage/JS-readable cookie.
- Given belum login, When akses `/student`, Then redirect `/login` tanpa flash konten.

## DoD + verifikasi runner (light)

`bun run build` → test guard (bila ada) → smoke manual login 4 akun seed (bareng hasil H2-E3) → setor. Catatan runner: ticket ini bergantung H2-E3 — jika BFF belum siap, verifikasi guard pakai mock session cookie & tandai di laporan.
