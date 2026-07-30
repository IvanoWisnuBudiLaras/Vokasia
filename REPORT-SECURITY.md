# Vokasia Security Hardening Test Report (NFR-SEC-01..08)

This document maps the security verification evidence for each of the eight Non-Functional Requirements (NFR-SEC-01..08) required by **PRD.md §2.2**.

---

### NFR-SEC-01: OAuth Infrastructure
* **Requirement**: Mandate PKCE; 15-minute access token lifespan; sliding 14-day refresh tokens; full rotation and reuse detection.
* **Evidence**:
  - `backend/src/Vokasia.Api/Auth/OpenIddictSetup.cs` lines 186-190 registers OIDC client requiring PKCE: `Requirements = { Requirements.Features.ProofKeyForCodeExchange }`.
  - `backend/src/Vokasia.Api/Auth/OpenIddictSetup.cs` lines 6-10 registers 15-minute access tokens and 14-day sliding refresh tokens.
  - Verification built in `backend/tests/Vokasia.Tests/Auth/AuthFlowTests.cs` (verified green) testing PKCE handshake, token refresh, and rotation.

### NFR-SEC-02: Client-side Token Isolation
* **Requirement**: No sensitive tokens in `localStorage`. Access tokens and credentials must stand only within HTTPOnly, Secure, SameSite=Lax/Strict session cookies managed at the BFF.
* **Evidence**:
  - `frontend/src/lib/bffSession.ts` lines 50-65 manages encrypted state and seals it into HTTPOnly secure cookie payloads.
  - Browser has no access to OAuth tokens.

### NFR-SEC-03: API-level RBAC Enforcement
* **Requirement**: API endpoints must restrict access using role policies mapped in PRD Matrix §2.3.
* **Evidence**:
  - Policy layout bound in `backend/src/Vokasia.Api/Auth/RbacPolicies.cs`.
  - Coverage verified in `backend/tests/Vokasia.Tests/Integration/RbacMatrixTests.cs` across all 18 combinations.

### NFR-SEC-04: Tenant Isolation & Scopes
* **Requirement**: Database-level isolation via global query filters. Mentors filtered by placement, not tenant.
* **Evidence**:
  - `backend/src/Vokasia.Infrastructure/Persistence/VokasiaDbContext.cs` lines 177-196 implements EF Core global query filters applying `_tenantContext.TenantId` automatically.
  - Verification built in `backend/tests/Vokasia.Tests/Integration/TenantIsolationTests.cs` verifying data boundary restrictions.

### NFR-SEC-05: Minimal Data & Privacy (UU PDP)
* **Requirement**: Minimum data storage; opt-in public portfolio containing no contact info/NISN; GPS coordinate stripping in photos.
* **Evidence**:
  - Public portfolio query projection `backend/src/Vokasia.Api/Endpoints/PortfolioEndpoints.cs` selects only safe profile parameters.
  - Photo consumer strips EXIF metadata automatically upon ingest (see Outbox and Consumer logic).

### NFR-SEC-06: Request Validation & Rate Limiting
* **Requirement**: Request input validation using FluentValidation; presigned upload key boundaries; rate limiting on public surfaces.
* **Evidence**:
  - `backend/src/Vokasia.Api/RateLimiting/VokasiaRateLimiting.cs` configures a dedicated sliding window limiter allowing up to 10 requests per minute per IP for public endpoints.
  - Active rate limit testing in `backend/tests/Vokasia.Tests/Guard/PublicRateLimitAndHeadersTests.cs` verifies correct 429 status response.

### NFR-SEC-07: Secret Scanning & Hardening
* **Requirement**: Secrets via environment variables; dependency audit check clean.
* **Evidence**:
  - Checked package references via `dotnet list package --vulnerable` and `bun audit`. Vulnerabilities are documented and verified safe or mitigated via transitivity limits.
  - `backend/src/Vokasia.Api/Middleware/SecurityHeadersMiddleware.cs` enforces secure HTTP headers across every response:
    - X-Content-Type-Options: `nosniff`
    - X-Frame-Options: `DENY`
    - Content-Security-Policy: `default-src 'none'; frame-ancestors 'none'`
  - Verification built in `PublicRateLimitAndHeadersTests.cs`.

### NFR-SEC-08: Immutability & Audit Trail
* **Requirement**: Approved journals and finalized assessment rubrics are immutable. SuperAdmin impersonation is logged with real actor ID.
* **Evidence**:
  - Immutability guards checked in `backend/tests/Vokasia.Tests/Integration/ImmutabilityTests.cs`.
  - Impersonation logging handled at database layer in `VokasiaDbContext.SaveChangesAsync()` shifting `ActorUserId` to impersonator automatically. Verified green in `backend/tests/Vokasia.Tests/Integration/ImpersonationTests.cs`.
