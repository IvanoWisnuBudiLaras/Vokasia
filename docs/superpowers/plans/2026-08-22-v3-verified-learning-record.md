# Vokasia V3 Verified Learning Record Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the private V3 Verified Learning Record for official PKL placements while preserving all V2 assessment and public credential behavior.

**Architecture:** Implement a side-by-side relational `LearningRecord*` subsystem. Immutable placement snapshots and finalized revisions provide stable provenance; a shared report-query contract feeds the API, PDF, XLSX, and Worker paths. Existing tenant filters, audit/outbox, notification, export, BFF, and visual primitives are reused.

**Tech Stack:** .NET 10, EF Core/Npgsql, ASP.NET Minimal APIs, existing authorization policies, MassTransit/outbox/Worker, Hangfire scheduling, QuestPDF, BCL OOXML writer, Next.js 16, Bun, React, existing BFF client, Playwright.

**Spec:** `docs/superpowers/specs/2026-08-22-v3-verified-learning-record-design.md`

## Global Constraints

- Preserve the dirty V2/V2.1 checkout; do not reset, clean, mass-restore, commit, or push.
- Do not modify `DESIGN.md` or parallel-owned Portfolio, Certificate, CV, public-media, and print surfaces except for strictly necessary regression-compatible API typing.
- Keep V2 `Assessment`, weighted scores, and historical records readable; V3 never reads them as Learning Record observations.
- Every tenant-scoped V3 query/mutation must enforce `TenantId`; mentor access is assignment/placement scoped.
- No browser bearer tokens, localStorage/sessionStorage tokens, anonymous V3 endpoint, public Learning Record URL, or permanent private-file URL.
- New behavior is TDD: write one focused failing test, run it and observe the expected failure, implement the minimum, run it green, then refactor.
- Update `V3_IMPLEMENTATION_PROGRESS.md` before schema/migration operations and after each objective gate.
- No new package/dependency without Product Owner approval.

---

### Task 1: Slice 1 domain rules and entity contracts

**Files:**
- Create: `backend/src/Vokasia.Domain/Entities/LearningRecordEntities.cs`
- Create: `backend/src/Vokasia.Domain/Common/LearningRecordRules.cs`
- Modify: `backend/src/Vokasia.Domain/Common/Enums.cs`
- Create: `backend/tests/Vokasia.Tests/Guard/LearningRecordRulesTests.cs`

**Interfaces:**
- `LearningRecordRules.ValidateScore(int score)` throws/returns the repository’s established domain validation error for values outside 1..5.
- `LearningRecordRules.GetDueDate(LearningAssessmentStage stage, DateOnly start, DateOnly end)` returns midpoint or `end.AddDays(-7)`.
- `LearningRecordRules.GetOperationalState(...)` returns a stable enum/label mapping for not-due, due, overdue, and finalized.
- Entities expose `TenantId`, placement/stage relationships, immutable revision provenance, and no V3 `Weight` field.

- [ ] Write tests for 0/1..5/6 scores, midpoint, Final H-7, due/overdue/finalized states, negative-monitoring-note rules, and latest-finalized revision selection.
- [ ] Run `dotnet test backend/tests/Vokasia.Tests/Vokasia.Tests.csproj --filter FullyQualifiedName~LearningRecordRulesTests`; confirm the new tests fail because the V3 types/rules do not exist.
- [ ] Add the enums, entities, and pure rules with `DateOnly` semantics and 1..5 labels only.
- [ ] Run the focused tests and confirm green; run existing `AssessmentScoringTests` to prove V2 weighted scoring remains unchanged.
- [ ] Update the checkpoint with Slice 1 domain files and test output.

### Task 2: Slice 1 persistence, indexes, and non-destructive migration

**Files:**
- Modify: `backend/src/Vokasia.Infrastructure/Persistence/VokasiaDbContext.cs`
- Create: additive EF migration `AddLearningRecordFoundation` under `backend/src/Vokasia.Infrastructure/Migrations/` plus its generated designer file
- Modify: `V3_IMPLEMENTATION_PROGRESS.md`
- Create: `backend/tests/Vokasia.Tests/Guard/LearningRecordPersistenceTests.cs`

- [ ] Write failing persistence tests for the unique `(PlacementId, Stage)` assessment constraint, unique placement snapshot, tenant query filtering, and max-20 criterion validation.
- [ ] Run the focused persistence tests and observe failure against the missing DbSets/model.
- [ ] Add DbSets, relationships, max lengths, indexes for tenant/placement/stage/status/student/DUDI, and global tenant filters without removing V2 mappings.
- [ ] Record the migration name in the checkpoint before running EF migration generation.
- [ ] Generate the migration with `dotnet ef migrations add AddLearningRecordFoundation --project backend/src/Vokasia.Infrastructure --startup-project backend/src/Vokasia.Api` and inspect it for additive/non-destructive operations only.
- [ ] Apply migrations to a clean test database, run persistence tests, and confirm V2 legacy assessment rows remain readable.

### Task 3: Slice 1 template versioning, snapshot, and authorization API

**Files:**
- Create: `backend/src/Vokasia.Api/Endpoints/LearningRecordTemplateEndpoints.cs`
- Create: `backend/src/Vokasia.Api/Validation/LearningRecordTemplateValidators.cs`
- Create: `backend/src/Vokasia.Api/Security/LearningRecordAuthorization.cs`
- Modify: `backend/src/Vokasia.Api/Program.cs`
- Create: `backend/tests/Vokasia.Tests/Assessment/LearningRecordTemplateEndpointsTests.cs`
- Create: `backend/tests/Vokasia.Tests/Integration/LearningRecordAuthorizationTests.cs`

- [ ] Write failing endpoint tests for DUDI-scoped create/update/activate, >20 criteria rejection, old active version immutability, placement snapshot creation, same snapshot reuse, and cross-tenant denial.
- [ ] Run the focused tests and confirm failure because the routes and V3 tables are absent.
- [ ] Implement template lifecycle endpoints with active-version immutability and explicit DUDI/mentor assignment checks; allow TenantAdmin governance only where the PRD permits.
- [ ] Implement placement snapshot creation as an idempotent transaction that copies the selected active template and criteria exactly once; Middle and Final resolve that snapshot, never the mutable template.
- [ ] Run focused unit/integration tests and existing V2 rubric tests together.
- [ ] Pass the Slice 1 gate: build, targeted tests, clean migration application, relevant V2 tests, and checkpoint update.

### Task 4: Slice 2 Mentor assessment API and finalization

**Files:**
- Create: `backend/src/Vokasia.Api/Endpoints/LearningAssessmentEndpoints.cs`
- Create: `backend/src/Vokasia.Api/Validation/LearningAssessmentValidators.cs`
- Create: `backend/src/Vokasia.Infrastructure/Queries/LearningRecordQueryService.cs`
- Modify: `backend/src/Vokasia.Api/Program.cs`
- Create: `backend/tests/Assessment/LearningAssessmentEndpointsTests.cs`
- Create: `backend/tests/Integration/LearningAssessmentFlowTests.cs`

- [ ] Write failing tests for Middle/Final stage state, required score/overall note, optional comment/evidence, approved-evidence ownership, Final availability without Middle, idempotent finalize, finalized lock, and Mentor assignment denial.
- [ ] Run the tests to observe the expected missing-route/domain failures.
- [ ] Implement draft load/save and per-criterion evidence association against approved journals/evidence only; do not load media originals in list/detail metadata queries.
- [ ] Implement transactional finalization that creates one immutable revision and audit event, with duplicate/retry protection and no weighted combined score.
- [ ] Expose Middle context while completing Final without copying scores.
- [ ] Run focused tests, existing assessment immutability/RBAC tests, and backend build.

### Task 5: Slice 2 reminders and Mentor UI

**Files:**
- Create: `backend/src/Vokasia.Worker/Jobs/LearningRecordReminderJobs.cs`
- Modify: `backend/src/Vokasia.Worker/Program.cs`
- Create: `frontend/src/app/(mentor)/mentor/perkembangan/page.tsx`
- Create: `frontend/src/app/(mentor)/mentor/perkembangan/[placementId]/page.tsx`
- Create: `frontend/src/app/(mentor)/mentor/perkembangan/[placementId]/MentorLearningAssessment.tsx`
- Create: `frontend/src/components/learning-record/AssessmentStageStatus.tsx`
- Create: `frontend/src/components/learning-record/ApprovedEvidencePicker.tsx`
- Modify: `frontend/src/lib/apiTypes.ts`
- Create: `frontend/src/app/(mentor)/mentor/perkembangan/MentorLearningAssessment.test.tsx`

- [ ] Write failing frontend tests for stage labels, missing-score/overall-note errors, read-only finalized state, reopened correction state, evidence selection, and no score auto-copy from Middle to Final.
- [ ] Run `bun test src/app/(mentor)/mentor/perkembangan/MentorLearningAssessment.test.tsx` and observe failure before implementation.
- [ ] Implement responsive Mentor list/detail using existing BFF clients and DESIGN.md form/dialog grammar; mobile detail is full-page and evidence selection is a sheet/page rather than a dense desktop dialog.
- [ ] Implement reminder deduplication using `PlacementId + Stage + Due/Overdue + Recipient`, creating in-app and email work once.
- [ ] Run `bun test src/`, backend reminder/assessment tests, and capture current-build Slice 2 screenshots.

### Task 6: Slice 3 Student Perkembangan

**Files:**
- Create: `backend/src/Vokasia.Api/Endpoints/LearningRecordReadEndpoints.cs`
- Create: `frontend/src/app/(student)/student/perkembangan/page.tsx`
- Create: `frontend/src/app/(student)/student/perkembangan/[placementId]/page.tsx`
- Create: `frontend/src/app/(student)/student/perkembangan/LearningRecordOverview.tsx`
- Modify: `frontend/src/app/(student)/student/layout.tsx`
- Modify: `frontend/src/lib/apiTypes.ts`
- Create: `backend/tests/Integration/LearningRecordReadAuthorizationTests.cs`
- Create: `frontend/src/app/(student)/student/perkembangan/LearningRecordOverview.test.tsx`

- [ ] Write failing tests for process state before Middle, Middle current after finalization, Final current while Middle remains history, own-student access, anonymous/other-student denial, private evidence, and separate placement grouping.
- [ ] Run focused backend/frontend tests and confirm failure.
- [ ] Implement read models that return only finalized revisions, group by Placement with latest first, and never invent empty criterion rows or improvement judgments.
- [ ] Integrate navigation without exceeding the existing mobile destination limit; keep `/student/penilaian` V2 behavior intact until V3 read flow is proven.
- [ ] Run focused tests, `bun test src/`, and capture Slice 3 screenshots at required mobile/desktop sizes.

### Task 7: Slice 4 Teacher monitoring and exception integration

**Files:**
- Create: `backend/src/Vokasia.Api/Endpoints/TeacherMonitoringEndpoints.cs`
- Create: `backend/src/Vokasia.Api/Validation/TeacherMonitoringValidators.cs`
- Create: `frontend/src/app/(school)/app/perkembangan/page.tsx`
- Create: `frontend/src/app/(school)/app/perkembangan/TeacherLearningRecord.tsx`
- Create: `frontend/src/app/(school)/app/perkembangan/TeacherMonitoringTimeline.tsx`
- Create: `backend/tests/Integration/TeacherMonitoringTests.cs`
- Create: `frontend/src/app/(school)/app/perkembangan/TeacherMonitoringTimeline.test.tsx`

- [ ] Write failing tests for manual status selection, required negative-status reason, optional positive note, StudentVisible/Internal filtering, append-only follow-up, Teacher scope, Mentor denial, and overdue exception read-only behavior.
- [ ] Run focused tests and observe expected failures.
- [ ] Implement monitoring event creation/list/follow-up with append-only records and role-specific visibility; do not auto-create status from scores.
- [ ] Add overdue Mentor assessment findings to the Teacher workspace without score mutation controls.
- [ ] Run backend/frontend focused tests and capture Slice 4 screenshots.

### Task 8: Slice 5 paginated report query and UI

**Files:**
- Modify: `backend/src/Vokasia.Infrastructure/Queries/LearningRecordQueryService.cs`
- Create: `backend/src/Vokasia.Api/Endpoints/LearningRecordReportingEndpoints.cs`
- Create: `frontend/src/app/(school)/app/laporan/perkembangan/page.tsx`
- Create: `frontend/src/app/(school)/app/laporan/perkembangan/DevelopmentReportTable.tsx`
- Create: `frontend/src/app/(school)/app/laporan/perkembangan/DevelopmentReportFilters.tsx`
- Create: `backend/tests/Integration/LearningRecordReportingTests.cs`
- Create: `frontend/src/app/(school)/app/laporan/perkembangan/DevelopmentReportTable.test.tsx`

- [ ] Write failing tests for page sizes 25/50/100, total count/pages, server-side search/filter/sort, Teacher scope, TenantAdmin scope, filter page reset, and absence of evidence media in list queries.
- [ ] Run focused tests and observe missing report behavior.
- [ ] Implement one semantic `LearningRecordReportQuery` used by API and future export paths; project required columns and use classic pagination.
- [ ] Preserve useful query state in URL parameters and render summary -> findings -> requested page only.
- [ ] Validate synthetic 25, 315, and feasible 5000-row datasets by query count/payload and browser page size, not arbitrary SLA claims.
- [ ] Run focused tests, `bun test src/`, and capture desktop/mobile/page-2/filtered/empty screenshots.

### Task 9: Slice 6 genuine PDF/XLSX export

**Files:**
- Modify: `backend/src/Vokasia.Domain/Entities/ExportEntities.cs`
- Modify: `backend/src/Vokasia.Api/Endpoints/GradeRecapEndpoints.cs` only where the shared `ExportRequest` contract is extended
- Create: `backend/src/Vokasia.Worker/Export/LearningRecordPdfDocument.cs`
- Modify: `backend/src/Vokasia.Worker/Consumers/ExportRequestedConsumer.cs`
- Modify: `backend/src/Vokasia.Worker/Export/MinimalXlsxWriter.cs` only if shared safety coverage requires it
- Create: `frontend/src/app/(school)/app/laporan/perkembangan/DevelopmentExportForm.tsx`
- Create: `backend/tests/Export/LearningRecordExportTests.cs`
- Create: `frontend/src/app/(school)/app/laporan/perkembangan/DevelopmentExportForm.test.tsx`

- [ ] Write failing tests for filter-aware PDF/XLSX requests, quantity/scope/sort, bounded PDF default, valid workbook row count/types, formula-injection-safe strings, tenant/export ownership, and large-export routing.
- [ ] Run focused tests and confirm failure before changing export production code.
- [ ] Extend the existing export request contract with report kind/query/scope/quantity; do not duplicate filter semantics in the Worker.
- [ ] Generate human-readable bounded PDF with title/context/summary/data and real selectable content; generate genuine XLSX with safe cell text and correct numeric/date types.
- [ ] Route above-threshold requests through the existing Worker/outbox/notification/download authorization path.
- [ ] Inspect generated PDF with a parser/render check and XLSX with a workbook reader/ZIP/XML assertion; do not accept HTTP 200 alone.
- [ ] Run focused tests and capture export-form screenshots.

### Task 10: Slice 7 integrity, seed data, and full regression

**Files:**
- Modify: `backend/src/Vokasia.Infrastructure/Seeding/DemoSeeder.cs`
- Modify: `backend/src/Vokasia.Infrastructure/Persistence/VokasiaDbContext.cs` only for measured indexes/final mappings
- Create: `backend/tests/Integration/LearningRecordIntegrityTests.cs`
- Create: `frontend/tests/e2e/v3-learning-record.spec.ts`
- Create: `artifacts/ui-review/v3/` screenshots from the current running build
- Modify: `V3_IMPLEMENTATION_PROGRESS.md`

- [ ] Write failing tests for TenantAdmin reopen reason/scope/audit, old-result correction state, re-finalize current result, Mentor A/B provenance, template/DUDI historical identity, date changes affecting only unfinalized due state, reminder idempotency, legacy final-only readability, and clean migration/seed.
- [ ] Run focused integrity tests and observe failures.
- [ ] Implement only the minimum fixes required by those failing tests; do not rewrite V2 or fabricate Middle data for legacy assessments.
- [ ] Seed deterministic active DUDI, Mentor, Student, Placement, template, approved evidence, Middle/Final, Teacher scope, monitoring, reopen, replacement, and large-report fixtures without uncontrolled duplication.
- [ ] Rebuild and run the full real stack on clean development/test volumes; verify Postgres, Redis, RabbitMQ, MinIO, Worker, API, Frontend, Caddy, and configured services.
- [ ] Run the final gate: `dotnet build backend/Vokasia.slnx`; full `dotnet test`; `cd frontend; bun run build`; `bun test src/`; `bun run lint`; `bun run test:e2e`; clean-volume migration -> seed -> health -> official E2E; `git diff --check`.
- [ ] Inspect browser console/network/privacy behavior and required screenshots; record exact totals, skipped tests, remaining gaps, and next entry point in the checkpoint.

## Completion rule

Do not claim V3 PASS until all seven slice gates and the final clean-runtime gate are evidenced. If a genuine product contradiction, destructive migration risk, unresolved security boundary, or architecture blocker remains, update the checkpoint precisely and stop with that decision request.
