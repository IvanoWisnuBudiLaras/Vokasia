# H7-E3 Playwright clean-state run

Start the normal clean environment, apply migrations, and run the deterministic `DemoSeeder` before starting Next.js and the API. Playwright talks to the public frontend through `PLAYWRIGHT_BASE_URL` and uses the normal BFF login path.

Required environment variables are local-only and must not be committed:

- `PLAYWRIGHT_BASE_URL`
- `E2E_SUPERADMIN_EMAIL`, `E2E_SUPERADMIN_PASSWORD`
- `E2E_TENANT_ADMIN_EMAIL`, `E2E_TENANT_ADMIN_PASSWORD`
- `E2E_TEACHER_EMAIL`, `E2E_TEACHER_PASSWORD`, `E2E_TEACHER_PLACEMENT_ID`, `E2E_TEACHER_ASPECT`, `E2E_TEACHER_NAME`
- `E2E_MAJOR_NAME`, `E2E_PERIOD_NAME`, optional `E2E_RUN_ID`
- `E2E_MENTOR_MAGIC_TOKEN`
- `E2E_STUDENT_EMAIL`, `E2E_STUDENT_PASSWORD`
- optional `E2E_CERTIFICATE_CODE`

The seeded names `DEMO-HEALTHY`, `DEMO-RED`, and `DEMO-REJECTED` are used as stable visible identifiers. Tests run serially because approval/rejection and storage/worker services share one clean seeded environment.

Commands after Bun dependencies have been legitimately installed:

```text
cd frontend
bun install
bun run test:e2e
bun run test:e2e:report
```

Runtime execution is currently NOT VERIFIED because Bun/Chromium are unavailable in this environment.
