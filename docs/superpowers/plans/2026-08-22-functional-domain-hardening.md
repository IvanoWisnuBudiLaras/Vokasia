# Plan: Functional and Domain Hardening

## Scope

Harden general/report/import behavior, private object uploads, Magic Link and session security, assessment/rubric realism, and school operations. Preserve the parallel agent's ownership of Public Portfolio, portfolio publication/media proxy, Certificate Verification/PDF, CV PDF, and their print styles. Do not change Certificate or CV export.

## Implementation steps

1. Add regression tests for the general export writer and student import boundaries: spreadsheet formula-like values stay text, CSV quoting is honored, file/row limits are enforced, duplicate or unknown-major rows are reported, and dry-run does not mutate data.
2. Implement the tested general export/import fixes with bounded input, tenant-owned master-data references, all-or-nothing persistence, and no hidden major creation. Keep import scope limited to the existing student CSV contract.
3. Add private-upload boundary tests and tighten the existing journal/private upload path: validate declared metadata before signing, enforce tenant/namespace ownership on attachment writes, verify bytes asynchronously, and remove failed private objects where the storage client supports cleanup. Leave public portfolio evidence delivery unchanged.
4. Make Magic Link exchange an atomic single-use claim, remove raw-link logging, and add race/replay coverage without putting raw tokens into durable events. Fix logout so the BFF session, auth cookies, and browser route state cannot leave a stale role view.
5. Add rubric domain fields for company scope, version/active state, criterion descriptions, and score comments. Resolve company-specific templates before the tenant default, create immutable versions when a template is referenced by an assessment, snapshot the selected template on assessment creation, and seed realistic teacher soft-skill criteria with weights summing to 100.
6. Update mentor/teacher scoring UI to show criterion descriptions and capture comments, remove fabricated operational metrics, and add operations empty states and tenant-owned major validation so the Jurusan dropdown cannot silently submit an invalid value.
7. Inspect private sharing and rich-text requirements against the locked dependency list. Implement only reusable/private behavior that is independent of the parallel-owned public paths; record unresolved cross-session or dependency-approval items explicitly instead of changing owned surfaces or adding an unapproved package.
8. Run focused backend/frontend tests first, then backend build/test, frontend type/build/unit checks, `frontend` Playwright E2E with Chromium, and targeted runtime/browser checks. Report exact totals, failures, skipped tests, screenshots, and unresolved dependencies.

## Verification gates

- No changes to the parallel-owned Portfolio, Certificate, CV, public media, or print files.
- No access token in browser storage; no raw Magic Link token in logs or outbox payloads.
- No public DTO exposes internal object-storage keys or presigned paths.
- `git diff --check` is clean for this session's changes.
- Completion claims are based only on fresh command output and rendered/runtime evidence.

## CROSS-SESSION DEPENDENCY

- Public Portfolio publication/media delivery, Certificate Verification/PDF, CV PDF, and their
  print/share surfaces remain owned by the parallel Slice 6/7 agent and were not changed here.
- The repository has no independent private-share resource/authorization contract outside those
  public surfaces. Adding one safely requires a product-level contract for authorized recipients,
  expiry, revocation, and file semantics; no permanent public URL was introduced as a shortcut.
- The locked frontend dependency set has no maintained rich-text editor. The existing plain-text
  journal representation and server-side tag stripping remain in force until Developer approves
  a package and representation change.
