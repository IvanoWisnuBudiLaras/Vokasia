# Security Report

Invitation lifecycle status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. `StaffInvitationToken` provides the shared random token format, hashed persistence, UTC expiry, and consumed marker. `SetInvitationPassword` uses a conditional update of the exact unconsumed token value inside a database transaction, followed by ASP.NET Identity password setup and an audit entry. Integration source covers valid, invalid, expired, replay, user binding, password policy, secret absence, provisioning paths, public setup response, and concurrent consumption.

QR status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. `QrCodeSvgGenerator.GeneratePng` is the standards-compliant QRCoder encoder used by `CertificatePdfDocument`; `QrCodeRoundTripTests` uses independent ZXing.Net ImageSharp decoding against that PNG representation. Physical PDF/device scanning remains unverified.

Seed status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. `DEMO-CERTIFICATE` is seeded through finalized assessment prerequisites and a normal `CertificateRequested` outbox request. `DemoCertificateSeedTests` verifies student, placement, period, final assessment, mentor/teacher scores, event payload, consumer-resolvable joins, and idempotency.

Authorization and tenant-isolation source status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. HTTP/resource evidence is in `AuthorizationResourceMatrixTests.cs`; `dotnet` execution remains unavailable.

Status is intentionally conservative because the current Windows environment lacks `dotnet` and `bun`.

| Requirement | Status | Evidence |
|---|---|---|
| NFR-SEC-01 authentication/session | PARTIAL | BFF/session source exists; runtime verification pending |
| NFR-SEC-02 authorization | PARTIAL | `RbacPolicies.cs`, assessment and placement ownership fixes; full suite pending |
| NFR-SEC-03 tenant isolation | PARTIAL | EF tenant filters and cross-tenant guards; runtime suite pending |
| NFR-SEC-04 input/upload safety | PARTIAL | validators and object-key policy exist; billing presign workflow pending |
| NFR-SEC-05 child-data minimization | PARTIAL | public DTOs omit NISN/contact; private portfolio and roster tests pending |
| NFR-SEC-06 auditability | PARTIAL | AuditLog writes exist; workflow coverage pending |
| NFR-SEC-07 transport/security headers | PARTIAL | HSTS/security headers/Caddy config updated; HTTPS runtime pending |
| NFR-SEC-08 dependency/release hygiene | NOT VERIFIED | dotnet/bun unavailable; audit not executed |

## Implemented source controls

Current classification for NFR-SEC-02 and NFR-SEC-03 supersedes the historical summary rows above: SOURCE COMPLETE / RUNTIME NOT VERIFIED, based on the HTTP/resource matrix in `AuthorizationResourceMatrixTests.cs`.

- Staff invitation stores only a SHA-256 token hash plus expiry in `AspNetUserTokens`; password activation deletes the token row and writes an `AuditLog` entry.
- Placement and assessment endpoints derive student, teacher, and mentor scope from persisted ownership.
- Billing upload keys are generated server-side under `tenant/{tenantId}/invoices/{invoiceId}/`.
- Browser authentication remains BFF-based; CORS and CSP use explicit origins.

## Authorization negative matrix

| Attack | Expected | Source/test evidence |
|---|---|---|
| Student → TenantMember roster | DENY | `RbacPolicies.cs`, `RbacPolicyTests.cs` |
| Teacher A → Teacher B assessment write | DENY | `AssessmentEndpoints.cs`; runtime test pending |
| Mentor A → Mentor B placement | DENY | `MentorOwnPlacement` resource policy |
| Student A → Student B placement/assessment | DENY | placement/assessment ownership guards |
| Tenant A → Tenant B resource | DENY/404 | EF tenant scope and endpoint guards |
| TenantAdmin → SuperAdmin escalation | DENY | school-user role whitelist |

## Runtime gates

## Persona UX source status

SOURCE COMPLETE FOR MVP FLOWS / RUNTIME NOT VERIFIED. Student, Mentor, Teacher, TenantAdmin, and SuperAdmin landing surfaces now expose role-specific next actions and avoid generic dashboard-only presentation. Material Symbols Rounded is the sole migrated icon family and meaningful icons have accessible labels. Frontend build, lint, and browser verification remain NOT VERIFIED because Bun is unavailable.

## H7-E3 Playwright source status

SOURCE COMPLETE / RUNTIME NOT VERIFIED. `frontend/tests/e2e/personas.spec.ts` covers provisioning/invitation state, billing presigned upload, teacher assessment mutation, mentor magic-link approval/rejection, and student journal/photo/revision/portfolio/verification flows. Browser policy failures are collected explicitly. No browser token injection, temporary password, manual object key, arbitrary row selection, or blind sleep is used.

## Release verification tooling status

SOURCE COMPLETE / RUNTIME NOT VERIFIED. `scripts/verify-release.ps1` is the single documented entry point. It records machine-readable and Markdown gate status, checks required executables and configuration, waits for the API and Compose services, runs backend/frontend/browser/performance/accessibility/restore/security gates, and preserves timestamped evidence under ignored `artifacts/release/`. Explicitly skipped required gates are incomplete and produce a nonzero exit code; no unexecuted result is reported as PASS.

TenantAdmin student/staff/DUDI/placement and Teacher journal/comment/visit/assessment source workflows are implemented and covered by frontend behavioral tests; browser execution remains NOT VERIFIED.

`dotnet build`, `dotnet test`, frontend build/lint, Playwright, load, Lighthouse, clean seed, and backup/restore are **NOT VERIFIED** until executed on a machine with the required toolchain.
