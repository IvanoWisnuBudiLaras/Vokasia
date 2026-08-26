# Vokasia V3 Verified Learning Record Design

**Status:** Approved by Product Owner on 2026-08-22

**Source of truth:** `Vokasia-PRD-V3-Verified-Learning-Record (1).md`

## Goal

Add an internal, private Verified Learning Record for official PKL placements. Industry Mentors submit exactly two formal observations, Middle and Final, against one immutable placement template snapshot. Students and authorized Teachers can read factual progression; Teachers can add separate monitoring events; TenantAdmins can govern reopening; authorized reporting surfaces use server-side pagination and genuine PDF/XLSX export.

## Non-negotiable boundaries

- Existing V2 weighted `Assessment`, `AssessmentScore`, Teacher scoring, certificates, portfolio, CV, journal, authentication, BFF, and public-media behavior remain readable and protected.
- V3 must not calculate weighted scores, a combined development score, cross-DUDI competency mappings, AI interpretations, or public Learning Records.
- All V3 tenant-scoped entities carry `TenantId` and receive an EF global query filter. Mentor scope is placement/assignment-based and is checked by endpoint authorization.
- Browser authentication remains BFF same-origin with httpOnly cookies. No access or refresh token enters browser storage.
- No dependency/package is added without Product Owner approval.
- `DESIGN.md` is frozen. V3 inherits its Clean Coastal, accessible, text-first, responsive grammar.
- All new behavior is developed test-first: a new test must fail for the intended missing behavior before production code is written.

## Architecture decision

V3 is a side-by-side relational subsystem. The current V2 `Assessment` name and schema are retained for historical compatibility; V3 uses explicit `LearningRecord*` names so legacy weighted Teacher/Mentor semantics cannot leak into the new record.

### Domain entities

The exact EF entities may be named with the `LearningRecord` prefix, but they must implement these contracts:

| Entity | Required responsibility |
|---|---|
| `LearningRecordTemplate` | Tenant/DUDI-owned template version with Draft/Active status, immutable after use, and no weights. |
| `LearningRecordTemplateCriterion` | Template criterion name, rubric/description, sort order, active flag; maximum 20 per version. |
| `PlacementLearningRecordSnapshot` | One placement-owned copy of the selected template identity, DUDI context, and criteria. Middle and Final reference this row only. |
| `PlacementLearningRecordCriterionSnapshot` | Immutable criterion name, rubric, and order used by the placement. |
| `LearningAssessment` | One row per `(PlacementId, Stage)` with Draft/Finalized/Reopened state and current draft ownership. A unique database constraint prevents duplicate stages. |
| `LearningAssessmentDraftCriterion` | Mutable pre-finalization score/comment/evidence selection for one assessment criterion. Score is nullable while draft and must be 1..5 to finalize. |
| `LearningAssessmentRevision` | Immutable finalized observation with evaluator identity/display snapshot, overall note, finalized timestamp, stage, and link to the prior placement snapshot. |
| `LearningAssessmentRevisionCriterion` | Immutable score, comment, and criterion identity for a finalized revision. |
| `LearningAssessmentCriterionEvidence` | Per-criterion link to an approved journal/evidence reference; server validates placement, student, tenant, and Approved status. |
| `TeacherMonitoringEvent` | Append-only Teacher status/note event with StudentVisible/Internal visibility and optional follow-up link/context. |
| `AssessmentReminderDelivery` | Unique due/overdue delivery key for placement, stage, reminder type, and recipient; makes in-app/email reminder creation idempotent. |

Use the existing `AuditLog` for template activation, assessment finalization/re-finalization, reopen, and sensitive export events unless a missing field prevents actor/time/reason/tenant provenance.

### Enums and pure rules

V3 defines only `Middle` and `Final` stages; `Draft`, `Finalized`, and `Reopened` are persisted lifecycle states. Operational `NotDue`, `Due`, `Overdue`, and `Complete` are computed for unfinalized assessments from existing project date semantics:

- Middle due point: midpoint between `Placement.StartDate` and `Placement.EndDate`.
- Final due point: `Placement.EndDate - 7 days`.
- Final availability never depends on Middle completion.
- A finalized result is not changed by later placement-date changes.

`LearningRecordRules` is a pure domain service exposing score validation, midpoint/final due dates, operational state, monitoring-note requirements, and score-label mapping (`1 Sangat Kurang` through `5 Sangat Baik`). It does not read the database or introduce a timezone subsystem.

### Finalization and reopen semantics

The mutable `LearningAssessment` holds the current draft and a nullable pointer to the latest immutable `LearningAssessmentRevision`. Finalization validates every score, the overall note, and evidence ownership, then inserts one revision and points the assessment at it in one transaction. A unique placement/stage constraint plus an idempotency/concurrency check makes retries safe.

TenantAdmin reopen records actor, time, tenant, stage, assessment, and required reason in `AuditLog`, changes the assessment to `Reopened`, and leaves the latest revision pointer unchanged. The Learning Record continues to read the old revision and displays `Sedang diperbaiki`; draft corrections become current only after a new revision is finalized.

### Authorization and tenant isolation

The API performs object-level checks in addition to EF filters:

- IndustryMentor: assigned placement and authorized DUDI template only.
- Student: own linked student record only.
- Teacher: assigned-student/placement scope only; no V3 score mutation.
- TenantAdmin: same-tenant read/report/reopen governance.
- Evidence: approved journal/evidence must match the assessment placement/student/tenant.

No anonymous V3 route is created. IDs supplied by another tenant return the repository’s established 404/403-safe behavior without exposing authorization claims or storage internals.

### API and shared query boundaries

Add a V3 endpoint module rather than extending V2 score routes:

- template list/create/update/activate and placement snapshot reads;
- placement stage state, draft load/save, evidence selection, finalize, finalized history, and TenantAdmin reopen;
- private student/mentor/teacher Learning Record reads;
- Teacher monitoring timeline/create/follow-up;
- paginated report query and export request.

The report UI, PDF generator, XLSX generator, and large-export worker consume the same `LearningRecordReportQuery` (tenant scope, period, DUDI, search, stage/status, monitoring status, sort, page/limit). List queries project only report fields and do not load evidence media blobs.

Reuse the current `ExportRequest`, outbox, Worker, QuestPDF, MinIO ownership checks, and formula-safe `MinimalXlsxWriter` infrastructure. Exports are filter-aware and quantity-aware; large quantities use the existing background request/notification/download path.

## Frontend integration

- Mentor: extend the existing assessment workspace with Middle/Final stage state, shared criterion form, approved evidence picker, Middle context while filling Final, and locked/reopened states.
- Student: add `/student/perkembangan` without exceeding the existing mobile navigation limit; show process state before Middle and placement-grouped factual progression after finalization.
- Teacher: add scoped Learning Record reading and append-only monitoring controls; no Teacher score input.
- TenantAdmin/reporting: add `/app/laporan/perkembangan` with summary, findings, server-side search/filter/sort/pagination, and a small export configuration surface.
- Use existing BFF `fetcher`/`apiClient`, accessible labels, semantic status text, 44px mobile targets, mobile full-page detail, and desktop operational table/panel grammar.

## Delivery and verification

The implementation proceeds in seven sequential slices:

1. Domain entities, pure rules, template versioning, placement snapshot, authorization, non-destructive migration, and backend tests.
2. Mentor Middle/Final forms, evidence, finalization lock, reopen-ready state, and idempotent reminders.
3. Student Perkembangan and private multi-placement history.
4. Teacher monitoring and overdue exception integration.
5. Server-side Learning Record reporting and URL-preserved query state.
6. Genuine bounded PDF/XLSX export and large-export routing.
7. Reopen/re-finalize, mentor replacement, date-change, historical identity, clean DB/seed, security, performance, browser, and full regression gates.

Every slice updates `V3_IMPLEMENTATION_PROGRESS.md` before and after risky changes. Required evidence is fresh source/test/runtime evidence; screenshots, HTTP 200, unit tests, or previous reports alone cannot establish completion.
