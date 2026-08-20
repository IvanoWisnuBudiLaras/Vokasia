# Release verification

The single release entry point is `./scripts/verify-release.ps1`. It derives the repository root from its own location, records a dirty/clean working-tree state, writes evidence to `artifacts/release/<UTC timestamp>/`, and returns non-zero when a required gate fails.

Preparation on a developer or CI machine:

1. Install .NET SDK, Bun, Docker Desktop, k6, Lighthouse CLI, and Trivy. The security gate runs `dotnet list package --vulnerable`, `bun audit --json`, and a HIGH/CRITICAL Trivy filesystem scan.
2. From `frontend/`, run `bun install` once and review the generated `bun.lock` diff. The release script subsequently uses `bun install --frozen-lockfile`.
3. Configure non-secret public URLs and release test variables in the environment, including `NEXT_PUBLIC_APP_URL`, `API_PUBLIC_URL`, `API_INTERNAL_URL`, `Cors__AllowedOrigins__0`, `MINIO_PUBLIC_URL`, `K6_JOURNAL_URL`, `K6_BEARER_TOKEN`, `K6_SLOT_IDS` (a comma-separated pool of valid, unsubmitted deterministic slot IDs), and `LIGHTHOUSE_PORTFOLIO_SLUG`.
4. Run `pwsh ./scripts/verify-release.ps1 -CleanState -AllowDirty`.

`-CleanState` explicitly runs project-scoped `docker compose down --volumes --remove-orphans`; it destroys the local Vokasia compose database and service volumes before rebuilding, migrating, and seeding. Omit it only for a non-destructive verification of an already prepared environment. `-SkipLoad`, `-SkipLighthouse`, `-SkipSecurity`, and `-SkipRestore` mark those gates `SKIPPED`; they do not become PASS.

The load gate is a k6 constant-arrival-rate scenario at 50 journal submissions/second for five minutes, using the real journal submit DTO, configured bearer token, and a supplied pool of valid unsubmitted slots. The API must provide enough isolated slots for the requested run; the script does not fabricate a test endpoint or reset production data. Lighthouse checks `/student` and `/p/<slug>` in mobile mode with performance >=85 and accessibility >=90 and stores JSON plus HTML reports. Backup/restore creates a fixed project verification database `vokasia_restore_verify`, never restores over the source database, compares source/restored counts for core tables, and rejects empty critical tables.

Every gate is recorded as `PASS`, `FAIL`, `SKIPPED`, or `NOT_RUN` in `summary.json` and `summary.md`. Explicitly skipped required gates make the command exit non-zero; a successful release run requires every required gate to be `PASS`. Evidence is written under the ignored `artifacts/release/` directory. The script refuses a dirty tree unless `-AllowDirty` is explicit, and `-CleanState` only removes volumes belonging to this compose project.

`frontend/bun.lock` is intentionally not edited by this tooling task. If it does not match `frontend/package.json`, run `cd frontend; bun install`, review the lockfile diff, and only then use the release script's frozen install gate.

The generated bundle result is an upper-bound measurement of the built `.next/static` payload, not a route-perfect transfer trace; it is retained as a conservative regression signal and is not represented as a measured `/student` payload claim.
