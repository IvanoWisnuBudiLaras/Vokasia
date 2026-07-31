# VOK-H1-E3 — OpenIddict + Identity + threat model

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-3 `backend/src/Vokasia.Api` | `h1-eng3-openiddict` | GPT-5.4 Thinking | **max** | M0 | PRD FR-AUTH-01/02, §2.4 auth flow, AGENTS.md §3 |

## Tugas

OAuth server internal (OpenIddict, Authorization Code + PKCE wajib) + ASP.NET Identity di `Vokasia.Api`, dengan JWT access 15 menit + refresh token. Plus threat model 1 halaman sebagai acuan review security seluruh sprint.

## Implementasi

### 1. Identity — `Auth/IdentitySetup.cs`
- `AddVokasiaIdentity(IServiceCollection s)` — tujuan: Identity dengan `AppUser`; password policy wajar (min 8); lockout 5 gagal/5 mnt; user store EF.
- `VokasiaClaimsFactory.GenerateClaimsAsync(AppUser user)` — tujuan: claims `sub`, `tenant_id` (null utk SuperAdmin/Mentor), `role`, `name`; sumber tunggal isi token — RBAC & filter H2-E3 bergantung padanya.

### 2. OpenIddict — `Auth/OpenIddictSetup.cs`
- `AddVokasiaOpenIddict(IServiceCollection s, IConfiguration cfg)` — tujuan: server flow **authorization code + PKCE (wajib, `RequireProofKeyForCodeExchange`)** + refresh token flow; access token lifetime **15 menit**, refresh 14 hari sliding; signing/encryption key dari env (dev: ephemeral + warning); endpoint `/connect/authorize`, `/connect/token`, `/connect/logout`.
- `AuthorizationController.Authorize(OpenIddictRequest req)` — tujuan: validasi client+redirect URI+PKCE, autentikasi user (cookie login form sederhana), terbitkan code.
- `AuthorizationController.Exchange(OpenIddictRequest req)` — tujuan: tukar code→(access JWT 15m + refresh); grant refresh→token baru (rotasi penuh di H2-E3 sisi BFF/Redis).
- `SeedOAuthClientsAsync(IServiceProvider sp)` — tujuan: registrasi client `vokasia-bff` (confidential, redirect `http://localhost:3000/api/auth/callback`, scope `openid profile api offline_access`) — idempoten.

### 3. Threat model — `backend/docs/threat-model.md` (1 halaman)
- Tujuan: aset (data anak, token, nilai), aktor jahat (siswa iseng, tenant lain, anonim, mentor palsu), permukaan (login, magic link, upload, endpoint publik), mitigasi → map ke NFR-SEC-01..08. Jadi checklist review VPM.

### 4. Test — `Vokasia.Tests/Auth/`
- `PkceRequiredTest` — flow tanpa PKCE → ditolak. · `TokenLifetimeTest` — access exp = 15 mnt. · `ClaimsContentTest` — token memuat sub/tenant_id/role. · `ClientSeedIdempotentTest`.

## Acceptance Criteria

- Given client BFF ter-seed, When code+PKCE flow lengkap, Then access(15m)+refresh terbit.
- Given flow tanpa PKCE / redirect URI salah, Then ditolak eksplisit.
- Given token, When di-decode, Then claims `sub, tenant_id, role` sesuai user.
- Threat model diserahkan & disetujui VPM (bagian gate M0).

## DoD + verifikasi runner (max)

Build+test → jalankan 4 test auth → negative check manual via curl (tanpa PKCE, client_id palsu → 4xx) → audit butir AGENTS §3 (JWT 15m ✓, tanpa token di browser storage — belum ada FE, catat N/A) → PROMPT D self-check → setor + threat-model.md.
