# H7-E3 Playwright Gate Execution Plan

> **For agentic workers:** Execute inline in the current session. Do not dispatch subagents, commit, push, reset, revert, recreate volumes, or run `docker compose down -v`.

**Goal:** Execute the repository-defined five-persona Playwright gate against the already-running Docker Compose application, repair only reproduced defects, and produce fresh acceptance evidence.

**Architecture:** Treat the current Compose stack and its seeded PostgreSQL data as the real system under test. Drive authentication through the public Next.js BFF routes, keep one isolated Playwright browser context per test, and diagnose failures across browser, BFF, API, worker, and database boundaries before changing code.

**Tech Stack:** Playwright 1.62, Bun, Next.js 16, ASP.NET Core .NET 10, PostgreSQL, Redis, RabbitMQ, MinIO, Docker Compose.

**Spec:** `docs/ticket/VOK-H7-E3.md` plus the session task contract attached by the Developer.

## Global Constraints

- Preserve the previously verified backend, vulnerability-audit, Compose, migration-owner, Caddy, and frontend-build results unless new runtime evidence proves regression.
- Execute all five personas as their real roles through the real Compose runtime; no fake backend or authorization bypass.
- Do not skip/fixme/delete tests, weaken assertions, add arbitrary sleeps, expose bearer tokens, or share credentials across personas.
- Repair one observed root cause at a time and run a focused reproduction after each change.
- Add no dependency and make no commit or push.
- Preserve every pre-existing dirty worktree change.

---

### Task 1: Freeze the concrete gate contract

**Files:**
- Read: `frontend/playwright.config.ts`
- Read: `frontend/package.json`
- Read: `frontend/tests/e2e/personas.spec.ts`
- Read: `frontend/tests/e2e/README.md`
- Read: `backend/src/Vokasia.Infrastructure/Seeding/DemoSeeder.cs`

**Interfaces:**
- Consumes: the running Compose services and deterministic demo seed.
- Produces: exact command, base URL, environment-variable map, serial/parallel mode, and five persona workflows.

- [ ] Record `bun run test:e2e` as the package-defined entrypoint and `frontend/tests/e2e/personas.spec.ts` as the current gate file.
- [ ] Resolve each required `E2E_*` value from checked-in seed definitions or read-only runtime data; never print password/token values in status output.
- [ ] Confirm whether `PLAYWRIGHT_BASE_URL` is wired into `playwright.config.ts`; treat a mismatch as evidence only after the real command reproduces it.

### Task 2: Prove the existing environment is eligible

**Files:**
- Read: `docker-compose.yml`

**Interfaces:**
- Consumes: current Docker Compose state.
- Produces: pre-run service and HTTP status evidence.

- [ ] Run `docker compose ps -a` and verify postgres, mailpit, minio, rabbitmq, redis, worker, api, frontend, and caddy are healthy/running.
- [ ] Run HTTP probes for the API health endpoint, `http://localhost:3000`, and `https://localhost/` without recreating services or volumes.

### Task 3: Execute the real suite and isolate the first failure

**Files:**
- Test: `frontend/tests/e2e/personas.spec.ts`
- Artifacts: `frontend/test-results/`
- Artifacts: `frontend/playwright-report/`

**Interfaces:**
- Consumes: resolved local-only `PLAYWRIGHT_BASE_URL` and `E2E_*` environment variables.
- Produces: Playwright counts and the first meaningful browser/runtime failure.

- [ ] From `frontend/`, run `bun run test:e2e` with the resolved environment variables.
- [ ] Read the complete first failure, trace, screenshot, video, failed request, and relevant browser console evidence.
- [ ] Query API/worker logs or database state only when the failure crosses that boundary.
- [ ] Classify the failure as test, product, seed/fixture, authentication/session, authorization, selector/accessibility, eventual-consistency, or environment/config.

### Task 4: Repair reproduced failures sequentially with TDD

**Files:**
- Modify only the file proven to own the root cause.
- Test: prefer a focused case in `frontend/tests/e2e/personas.spec.ts`; for isolated application logic, add or update the nearest existing unit test.

**Interfaces:**
- Consumes: one confirmed root-cause hypothesis at a time.
- Produces: the smallest behavior-preserving repair plus red/green evidence.

- [ ] Re-run the focused failing Playwright test unchanged to establish RED and confirm reproducibility.
- [ ] Compare the failing path with a working persona/page/helper in the same repository.
- [ ] Apply one minimal change using `apply_patch`; do not batch unrelated corrections.
- [ ] Re-run the focused test to establish GREEN.
- [ ] Continue with the next failure only after the focused case passes.

### Task 5: Run the final acceptance gates

**Files:**
- Verify: all files changed during this task.

**Interfaces:**
- Consumes: the repaired H7-E3 gate.
- Produces: fresh Playwright, Compose, backend, and whitespace evidence.

- [ ] Run the complete `bun run test:e2e` suite once after the last code change and capture total/passed/failed/skipped counts.
- [ ] Run `docker compose ps -a` and record the required service states.
- [ ] Run `dotnet test .\backend\Vokasia.slnx --no-build --logger "console;verbosity=minimal"` and capture total/passed/failed/skipped counts.
- [ ] Run `git diff --check` and report its exit status and any findings.
- [ ] Re-read the session acceptance checklist and report persona workflows, chronological root causes, changed files, and remaining non-blockers without claiming PASS unless every gate is green.
