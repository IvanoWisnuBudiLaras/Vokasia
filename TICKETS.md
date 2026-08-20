# Vokasia Tickets

R0 authorization source status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. The HTTP/resource regression matrix is implemented in `backend/tests/Vokasia.Tests/Integration/AuthorizationResourceMatrixTests.cs`.

Invitation lifecycle status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. TenantAdmin provisioning and school-staff invites use one passwordless setup-token flow with hashed persistence, expiry, atomic single-use consumption, and no plaintext password event payload.

QR status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. The certificate embeds the QRCoder PNG representation and has an independent ZXing.Net round-trip source test. Seed status: SOURCE COMPLETE / RUNTIME NOT VERIFIED. The deterministic `DEMO-CERTIFICATE` scenario follows the normal assessment/outbox/worker path; runtime worker generation remains pending.

Historical MVP tickets remain in git history. This file records the active release recovery track.

## R0 — Security Containment

**Status: PARTIAL — source guards implemented; full integration matrix NOT VERIFIED.**

- **ID:** R0-SEC-01
- **Problem:** role policies and resource ownership must resist cross-user, cross-role, and cross-tenant access.
- **User outcome:** each persona sees only authorized school, placement, assessment, and portfolio data.
- **Entry condition:** endpoint authorization is under review.
- **Primary flow:** authenticate, resolve identity, scope query/resource, perform action.
- **Failure/recovery flow:** return 403 or anti-enumeration 404; record sensitive mutations in AuditLog.
- **Security rules:** no tenant claim-only authorization; ownership comes from persisted resource and caller identity.
- **Implementation scope:** RBAC policies, placements, assessments, privacy regression tests.
- **Out of scope:** redesigning the domain schema.
- **Tests:** negative matrix for student, teacher, mentor, tenant, and escalation paths.
- **AC Given/When/Then:** Given a caller from another placement, when reading or mutating it, then access is denied.
- **Evidence required:** source locations and passing test output.
- **DoD:** no known resource leak remains and runtime tests are green.

## R1 — Core Workflow Repair

**Status: PARTIAL — billing presign, seed collection/scenarios, QR encoder, and invitation source flow implemented; runtime and complete scenario verification NOT VERIFIED.**

- **ID:** R1-WORK-01
- **Problem:** clean seed, invitation, billing proof, and certificate QR must be usable end to end.
- **User outcome:** demo users can complete workflows without internal object keys or temporary-password dead ends.
- **Entry condition:** security boundaries are defined.
- **Primary flow:** deterministic seed → invite/setup → upload proof → certificate verify.
- **Failure/recovery flow:** expired token, retryable upload, rejected proof, unknown certificate code.
- **Security rules:** single-use hashed tokens, scoped object keys, private storage, minimal verification output.
- **Implementation scope:** backend workflows, UI states, integration tests.
- **Out of scope:** payment gateway settlement.
- **Tests:** clean-state integration and QR encode/decode invariant.
- **AC Given/When/Then:** Given a clean database, when demo seed runs, then required personas and scenarios exist deterministically.
- **Evidence required:** counts, workflow traces, and test output.
- **DoD:** no manual ObjectKey entry and no reusable temporary password.

## R2 — Material Design System

**Status: PARTIAL — dependencies, constitution, and initial workflow primitives are present; full UI audit NOT VERIFIED.**

- **ID:** R2-UI-01
- **Problem:** operational UI needs a consistent Material 3 foundation without flattening domain workflows.
- **User outcome:** accessible, recognizable controls with persona-specific information architecture.
- **Entry condition:** R0 and R1 stable.
- **Primary flow:** domain component uses Material primitive and bundled Material Symbols Rounded icon.
- **Failure/recovery flow:** loading, empty, error, offline, and retry states are explicit.
- **Security rules:** no secrets in client bundle; no remote critical icon runtime.
- **Implementation scope:** tokens, primitives, domain components, DESIGN.md.
- **Out of scope:** all-Web-Components migration.
- **Tests:** lint, visual/browser, keyboard, 360px.
- **AC Given/When/Then:** Given a mobile student screen, when loaded at 360px, then it has one clear task CTA and no horizontal scroll.
- **Evidence required:** screenshots and browser checks.
- **DoD:** anti-AI-slop checklist passes.

## R3 — Persona UX Rebuild

**Status: SOURCE COMPLETE FOR MVP FLOWS / RUNTIME NOT VERIFIED — set-password and billing states remain source-only; persona landing workflows are implemented.**

Current MVP persona pass: SOURCE COMPLETE / RUNTIME NOT VERIFIED. Student now starts at today's task and revision recovery; Mentor at the approval queue; Teacher at ordered exceptions; TenantAdmin at operational follow-up; SuperAdmin at platform actions and health. Browser execution remains unverified because Bun is unavailable.

- **ID:** R3-UX-01
- **Problem:** student, mentor, teacher, tenant admin, and super admin need different interaction grammars.
- **User outcome:** each persona reaches the next useful action quickly.
- **Entry condition:** domain workflows are correct.
- **Primary flow:** task-first, queue-first, exception-first, operations-first, or platform-first screen.
- **Failure/recovery flow:** actionable errors and empty states preserve the next step.
- **Security rules:** UI visibility never replaces API authorization.
- **Implementation scope:** workflow screens and E2E selectors.
- **Out of scope:** decorative landing-page redesign.
- **Tests:** five persona E2E flows and accessibility checks.
- **AC Given/When/Then:** Given a mentor with pending approvals, when opening the app, then the approval queue is the primary task.
- **Evidence required:** Playwright trace and browser console/request logs.
- **DoD:** stable accessible selectors and mobile touch targets.

## R4 — Operations & Deployment

**Status: PARTIAL — CORS/CSP/Caddy source configuration updated; browser topology tests NOT VERIFIED.**

- **ID:** R4-OPS-01
- **Problem:** public/internal URLs, CORS, CSP, storage, and TLS must describe one network topology.
- **User outcome:** browser requests do not fail from origin-policy or mixed-content errors.
- **Entry condition:** URL contract is documented.
- **Primary flow:** browser same-origin BFF; direct browser storage only via exact public origin.
- **Failure/recovery flow:** rejected origin, failed preflight, failed upload, unhealthy dependency.
- **Security rules:** exact CORS origins, no wildcard authenticated credentials, no Docker DNS in browser URLs.
- **Implementation scope:** API middleware, CSP, compose, Caddy, env contract.
- **Out of scope:** adding a second proxy architecture.
- **Tests:** preflight integration and browser network assertions.
- **AC Given/When/Then:** Given an untrusted origin, when it sends preflight, then no CORS allowance is returned.
- **Evidence required:** response headers and browser logs.
- **DoD:** TLS and docs agree.

## R5 — Release Verification

**Status: SOURCE COMPLETE / RUNTIME NOT VERIFIED — five persona workflows, deterministic resource selectors, upload fixtures, browser policy guards, serial clean-state configuration, and run instructions are implemented. Execution requires the developer toolchain and clean services.**

TenantAdmin operations and Teacher student actions are now source-complete: `/app/operasi` covers student creation, staff invitation, DUDI proposal, and placement creation; teacher detail links to journal/comment, visit, and assessment routes.

- **ID:** R5-REL-01
- **Problem:** release readiness needs reproducible evidence, not claims.
- **User outcome:** developer can rerun build, E2E, performance, accessibility, and restore checks.
- **Entry condition:** R0–R4 implemented.
- **Primary flow:** clean state → seed → test → measure → backup/restore.
- **Failure/recovery flow:** preserve failed artifacts and mark gate NOT VERIFIED when tooling is unavailable.
- **Security rules:** dependency audit and secret scan are release gates.
- **Implementation scope:** Playwright, benchmark, Lighthouse, bundle measurement, backup/restore, dependency/security checks, CORS/health probes, evidence summary, and release runbook.
- **Out of scope:** automatic release tagging.
- **Tests:** dotnet, Bun frozen install/lint/unit/build, Docker readiness, Playwright, k6, Lighthouse, bundle, CORS, health, dependency scan, and restore.
- **AC Given/When/Then:** Given any failed gate, when release report is generated, then verdict is NOT RELEASE READY.
- **Evidence required:** exact commands and outputs.
- **DoD:** all required gates pass on the developer machine.
