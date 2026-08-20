# Vokasia Decisions

## Current release recovery decisions

- Authorization uses semantic `TenantMember` staff roles only; student access uses `StudentSelf` and persisted ownership.
- Placement ownership is identity-derived: teacher assignment, mentor assignment, or student-to-user linkage; query parameters only narrow results.
- Browser authentication remains BFF same-origin. Access tokens never enter browser storage.
- ASP.NET CORS uses exact `Cors:AllowedOrigins`; production does not use wildcard origins.
- Storage has separate internal and browser-public URLs. Docker DNS is never returned to browsers.
- Demo seed is deterministic and idempotent, with fictional identities and forced RAG/workflow scenarios.
- Staff invitation must use expiring, single-use setup tokens; reusable temporary passwords are not a valid UX.
- Billing proof uses backend-generated scoped object keys and presigned upload.
- Certificate QR uses a standards-compliant encoder and encodes the public `/verify/{certCode}` URL exactly.
- Material 3 is adopted as a hybrid: `@material/web` primitives plus React domain components.
- Critical icons use bundled Iconify Material Symbols Rounded; remote icon runtime is not a release dependency.
- DESIGN.md is the UI constitution and includes anti-AI-slop and persona grammar rules.
- Caddy owns TLS in the edge profile; real production uses a DNS name and ACME, while localhost is development-only.
- Staff setup tokens use ASP.NET Identity's existing `AspNetUserTokens` table: only a SHA-256 token hash and expiry are persisted, and the row is deleted after password activation.

Invitation lifecycle implementation: tenant-admin provisioning and school-staff invites share `StaffInvitationToken`; the raw 32-byte bearer exists only for email transport. Password setup claims the exact stored value with a conditional database update inside a transaction, replacing it with a consumed marker. The database is the race arbiter: one concurrent request succeeds and replay returns a used-invitation conflict. `EmailConfirmed` remains independent.

Certificate/demo decision: certificates use the normal finalized-assessment -> `CertificateRequested` outbox -> worker path. `DEMO-CERTIFICATE` is the primary tenant's fifth seeded student with completed placement, assessment finalization, all three rubric scores, and a deterministic request event; no fake Certificate row is inserted. Certificate PDFs embed the QRCoder PNG representation, with ZXing.Net independent ImageSharp decoding as a source-level round-trip proof. QR payload is only the configured public `/verify/{certCode}` URL.
- Release status remains conservative: source implementation is not runtime proof.
- Persona MVP surfaces follow distinct interaction grammar: Student task-first, Mentor queue-first, Teacher exception-first, TenantAdmin operations-first, and SuperAdmin platform-first. Shared MaterialIcon uses Material Symbols Rounded with accessible labels; collections are lists rather than automatic KPI-card grids.
- H7-E3 Playwright runs serially against clean deterministic seed data. Each persona receives separate credentials through environment variables; mentor uses the real magic-link token. Tests fail on relevant CORS/CSP/mixed-content/network/application errors and never inject browser tokens or use test login backdoors.
- TenantAdmin MVP operations use existing student, staff-invitation, company-proposal, and placement APIs behind one `/app/operasi` surface. Selectors expose names and labels; identifiers remain transport data only. Teacher detail links to the existing journal-comment, visit, and assessment routes rather than duplicating those APIs.
- H7 release verification has one PowerShell entry point, writes ignored timestamped evidence, refuses dirty trees unless explicitly allowed, uses project-scoped clean-state volumes, waits on Compose health instead of blind sleeps, and exits nonzero for failed or skipped required gates. Runtime results remain unverified until the developer toolchain executes it.
- Release evidence uses real configured resources: k6 requires a comma-separated pool of valid unsubmitted journal slots, Lighthouse audits `/student` and `/p/{slug}` in mobile mode, and backup/restore compares source and restored counts in a separate verification database. No gate fabricates PASS output.
