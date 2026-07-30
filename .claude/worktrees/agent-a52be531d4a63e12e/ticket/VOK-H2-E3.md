# VOK-H2-E3 — BFF token exchange + RBAC + tenant/placement filter + magic link

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-3 `frontend/` (BFF) + `backend/` | `h2-eng3-bff-rbac-magiclink` | GPT-5.4 Thinking | **ultra** | **M1** | PRD FR-AUTH-01..07, §2.3–2.4, NFR-SEC-01..04 |

## Tugas

Jantung security: BFF session (token hanya server-side), RBAC policy penuh, aktivasi tenant/placement filter + test isolasi, magic link mentor. **Prioritas bila terdesak: BFF → RBAC/filter → magic link** (magic link boleh geser pagi H3 — lapor, jangan diam).

## Implementasi

### 1. BFF — `frontend/src/app/api/auth/*` (route handlers, server-only)
- `GET /login → handleLogin(req)` — tujuan: mulai code+PKCE (generate verifier+challenge+state, simpan sementara di Redis) → redirect `/connect/authorize`.
- `GET /callback → handleCallback(req{code,state})` — tujuan: validasi state → tukar code (PKCE verifier) → simpan `{accessToken, refreshToken, exp, user}` di **Redis** key `sess:{sessionId}` → set cookie `vok_sess` httpOnly Secure SameSite=Lax → redirect `roleHome`.
- `GET /session → handleSession()` — tujuan: baca Redis → `{user:{id,name,role,tenantId}}`; **tanpa token di response**.
- `POST /logout → handleLogout()` — tujuan: hapus session Redis + revoke refresh di OpenIddict + clear cookie (revocation instan, FR-AUTH-04).
- `proxyWithBearer(req) → Response` (`app/api/proxy/[...path]`) — tujuan: satu-satunya jalur FE→API: ambil access dari Redis, tempelkan `Authorization: Bearer`, teruskan; 401 → coba refresh 1× → ulang.
- `refreshOnExpiry(sessionId)` — tujuan: **rotation**: refresh lama ditukar baru (simpan hash refresh terakhir); **reuse detection**: refresh lama dipakai lagi → revoke seluruh sesi user + audit `TokenReuseDetected`.

### 2. RBAC & filter — `backend/`
- `RegisterRbacPolicies(IServiceCollection s)` — tujuan: policy per baris matrix 2.3, nama baku: `"SaOnly","TenantAdmin","DeptHead+","Teacher+","MentorOwnPlacement","StudentSelf","TenantMember"` — dipakai semua endpoint (H2-E1 dst).
- `TenantResolutionMiddleware.Invoke(ctx, next)` — tujuan: isi `ITenantContext` dari claims (`tenant_id`,`sub`,`role`); SuperAdmin boleh override via header `X-Acting-Tenant` (tercatat audit).
- Aktivasi `ApplyTenantQueryFilters` (hapus marker H1) — tujuan: global filter hidup untuk SEMUA entitas tenant-scoped; entitas global (Company, Plan) dikecualikan eksplisit.
- `PlacementScopeHandler : AuthorizationHandler<PlacementScopeRequirement>` — tujuan: mentor hanya resource pada `Placement.MentorUserId == sub` (bukan per tenant); dipakai policy `MentorOwnPlacement`.
- `WriteAuditLog(actorId, actingAsId?, action, entity, entityId, metaJson)` (`IAuditWriter`) — tujuan: satu pintu audit (FR-X-01); dipanggil aksi auth sensitif mulai sekarang.

### 3. Magic link mentor — `backend/Auth/MagicLink/`
- `CreateMentorInvite(Guid placementId, string email) → InviteDto` — tujuan: buat `MentorInvite{TokenHash, ExpiresAt=+72h, UsedAt?}` — token mentah hanya di email; publish `MentorInvited` (outbox; email terkirim H4, sementara log dev).
- `ValidateMagicToken(string token) → InviteValidation` — tujuan: cek hash ada + belum expired + belum dipakai; gagal → alasan generik (jangan bocorkan mana yang salah).
- `ExchangeMagicToken(string token) → session mentor` — tujuan: tandai `UsedAt` (sekali pakai), buat/tautkan `AppUser` mentor, terbitkan session via BFF (tanpa password, FR-AUTH-03).

### 4. Test — `Vokasia.Tests/Security/` (inti ticket ini)
- `TenantIsolationTests` — user tenant A akses 6 endpoint utama dgn id milik tenant B → **404/403 semua**.
- `RbacPolicyTests` — sampel matrix per role (mis. Teacher approve jurnal → 403).
- `RefreshRotationTests` — reuse refresh lama → seluruh keluarga sesi tercabut.
- `MagicLinkTests` — dipakai 2× → tolak; >72 jam → tolak; happy path → session mentor.
- `RevocationTests` — logout/deactivate → request berikutnya 401 instan.

## Acceptance Criteria

- Semua test §4 hijau. Browser hanya berisi cookie httpOnly (NFR-SEC-02).
- Given SuperAdmin tanpa `X-Acting-Tenant`, When akses data tenant, Then hasil kosong (filter tetap bekerja).
- Given mentor login magic link, Then hanya melihat placement miliknya.

## DoD + verifikasi runner (ultra)

Terapkan bertahap (BFF → RBAC → magic link), suite per tahap → clean state penuh: `docker compose down -v` → up → migrate → seed → seluruh suite security → negative test manual (curl lintas tenant, refresh reuse) → PROMPT D → setor + bukti per AC.
