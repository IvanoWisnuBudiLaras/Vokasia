# Audit Vokasia v2 Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menutup temuan Audit v2 pada isolasi tenant, readiness worker, login publik, dokumentasi kontras, dan regression coverage tanpa mengubah OpenAPI, skema DB, atau dependency.

**Architecture:** Mekanisme header tenant warisan dihapus sehingga konteks tenant hanya berasal dari claim token/impersonation yang sudah ber-audit. Entity tenant yang terlewat memperoleh EF global query filter dan tes perilaku lintas tenant. Worker menerbitkan heartbeat readiness dari `HealthCheckService` (Postgres, Redis, MinIO, dan health bus MassTransit) ke file atomik yang diperiksa Compose. Perubahan UI tetap memakai token `DESIGN.md` dan CSP nonce yang sudah ada.

**Tech Stack:** .NET 10, EF Core 10, ASP.NET Core, MassTransit 8.5, MinIO SDK, xUnit, Next.js 16, React 19, Bun test, Docker Compose.

## Global Constraints

- `PRD.md` adalah sumber requirement; `DESIGN.md` berstatus beku dan nilai token tidak diubah.
- Tenant isolation wajib di EF global query filter; impersonation SuperAdmin tetap melalui token ber-claim `impersonator_id` dan AuditLog.
- Dilarang mengubah OpenAPI, migration/skema DB, atau menambah package/dependency.
- Server Components tetap default; browser tidak menyimpan access/refresh token.
- UI Bahasa Indonesia sederhana, target sentuh minimal 44 px, dan `prefers-reduced-motion` tetap dihormati.
- Tulis test sebelum perubahan perilaku dan jalankan targeted test merah lalu hijau.
- Worktree berisi perubahan milik user; jangan reset, stash, menghapus, atau commit perubahan sesi ini.

---

### Task 1: Tenant Context and Global Query Filters

**Files:**
- Modify: `backend/src/Vokasia.Api/Auth/TenantResolutionMiddleware.cs`
- Modify: `backend/src/Vokasia.Domain/Common/ITenantContext.cs`
- Modify: `backend/src/Vokasia.Infrastructure/TenantContext/AmbientTenantContext.cs`
- Modify: `backend/src/Vokasia.Domain/Entities/CompanyEntities.cs`
- Modify: `backend/src/Vokasia.Domain/Entities/PlatformAndBillingEntities.cs`
- Modify: `backend/src/Vokasia.Infrastructure/Persistence/VokasiaDbContext.cs`
- Create: `backend/tests/Vokasia.Tests/Security/TenantResolutionMiddlewareTests.cs`
- Modify: `backend/tests/Vokasia.Tests/Security/TenantIsolationTests.cs`

**Interfaces:**
- Consumes: authenticated claims `sub`, `role`, `tenant_id`, `impersonator_id`.
- Produces: `AmbientTenantContext` that cannot be changed by `X-Acting-Tenant`; filters for `TenantCompany`, `CompanySlot`, and `Invoice`.

- [ ] **Step 1: Write failing middleware regression test**

```csharp
[Fact]
public async Task SuperAdmin_LegacyActingTenantHeader_CannotChangeTenantContext()
{
    var requestedTenant = Guid.NewGuid();
    var ambient = new AmbientTenantContext();
    var http = AuthenticatedContext(UserRole.SuperAdmin);
    http.Request.Headers["X-Acting-Tenant"] = requestedTenant.ToString();
    var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

    await middleware.InvokeAsync(http, ambient);

    Assert.Null(ambient.TenantId);
    Assert.Equal(nameof(UserRole.SuperAdmin), ambient.Role);
}
```

- [ ] **Step 2: Write failing filter behavior test**

Seed two tenants for `TenantCompany`, `CompanySlot`, and `Invoice`, then query with tenant A and assert every returned `TenantId` equals tenant A. Also enumerate mapped entity types whose non-nullable `TenantId` property lacks `GetQueryFilter()` and assert the list is empty.

- [ ] **Step 3: Run targeted tests and confirm RED**

Run:

```powershell
dotnet test backend/tests/Vokasia.Tests/Vokasia.Tests.csproj --filter "FullyQualifiedName~TenantResolutionMiddlewareTests|FullyQualifiedName~Vokasia.Tests.Security.TenantIsolationTests"
```

Expected: legacy header changes ambient context and the three omitted entities leak cross-tenant rows.

- [ ] **Step 4: Remove the legacy header path**

Delete the `X-Acting-Tenant` branch and `IsSuperAdminActingAsTenant` property. Keep `tenant_id` and `impersonator_id` claim handling unchanged. Update comments so token impersonation is the only supported scoped SuperAdmin identity switch.

- [ ] **Step 5: Add the three filters**

Make `TenantCompany`, `CompanySlot`, and `Invoice` implement `ITenantScoped`, then add:

```csharp
b.Entity<TenantCompany>().HasQueryFilter(x =>
    !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
b.Entity<CompanySlot>().HasQueryFilter(x =>
    !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
b.Entity<Invoice>().HasQueryFilter(x =>
    !_tenantContext.TenantId.HasValue || x.TenantId == _tenantContext.TenantId);
```

- [ ] **Step 6: Run targeted tests and confirm GREEN**

Use the command from Step 3. Expected: all targeted tests pass.

### Task 2: Dependency-Aware Worker Readiness

**Files:**
- Create: `backend/src/Vokasia.Worker/Health/WorkerDependencyHealthChecks.cs`
- Create: `backend/src/Vokasia.Worker/Health/WorkerReadinessPublisher.cs`
- Modify: `backend/src/Vokasia.Worker/Program.cs`
- Create: `backend/tests/Vokasia.Tests/Worker/WorkerReadinessPublisherTests.cs`
- Modify: `docker-compose.yml`

**Interfaces:**
- Consumes: `HealthCheckService`, MassTransit health registration tagged `ready`, Postgres, Redis, MinIO bucket.
- Produces: `/tmp/vokasia-worker-ready`, containing a Unix timestamp refreshed only while all readiness checks are healthy.

- [ ] **Step 1: Write failing readiness publisher tests**

Use real `HealthCheckService` registrations with deterministic healthy/unhealthy `IHealthCheck` implementations. Assert a healthy report atomically writes a numeric timestamp and an unhealthy report removes a pre-existing readiness file.

- [ ] **Step 2: Run targeted tests and confirm RED**

Run:

```powershell
dotnet test backend/tests/Vokasia.Tests/Vokasia.Tests.csproj --filter "FullyQualifiedName~WorkerReadinessPublisherTests"
```

Expected: `WorkerReadinessProbe`/publisher does not exist.

- [ ] **Step 3: Implement dependency checks and publisher**

Move Postgres/Redis checks out of top-level `Program.cs`, add MinIO bucket existence check, tag all with `ready`, and register a hosted publisher. `WorkerReadinessProbe.CheckAndPublishAsync` calls:

```csharp
healthChecks.CheckHealthAsync(
    registration => registration.Tags.Contains("ready"),
    cancellationToken);
```

Only `HealthStatus.Healthy` writes the timestamp; degraded/unhealthy/exception removes the file. Write to a sibling temporary file and move with overwrite so Compose never reads a partial timestamp. Refresh every 10 seconds and delete the readiness file on shutdown.

- [ ] **Step 4: Replace Compose process check**

```yaml
healthcheck:
  test:
    - CMD-SHELL
    - >-
      test -s /tmp/vokasia-worker-ready &&
      now=$$(date +%s) &&
      ready=$$(cat /tmp/vokasia-worker-ready) &&
      [ $$((now - ready)) -le 30 ]
```

- [ ] **Step 5: Run targeted tests and confirm GREEN**

Use the command from Step 2. Expected: healthy-write and unhealthy-remove tests pass.

### Task 3: Login Ergonomics, Copy, Contrast, and UI Primitives

**Files:**
- Modify: `backend/src/Vokasia.Api/Middleware/SecurityHeadersMiddleware.cs`
- Modify: `backend/src/Vokasia.Api/Auth/AccountEndpoints.cs`
- Modify: `backend/tests/Vokasia.Tests/Auth/AccountLoginTests.cs`
- Modify: `frontend/src/lib/localReturnUrl.ts`
- Modify: `frontend/src/components/ui/ErrorState.tsx`
- Create: `frontend/src/components/ui/StatePrimitives.test.tsx`
- Modify: `frontend/src/app/globals.css`

**Interfaces:**
- Consumes: existing per-request CSP nonce and frozen `sekolah` tokens.
- Produces: accessible password reveal button; synchronized login audience copy; documented WCAG ratios; render coverage for non-dead-end UI states.

- [ ] **Step 1: Write failing login test**

Assert the real `/account/login` HTML has a `type="button"` reveal control with `aria-controls="password"`, `aria-pressed="false"`, a nonce-protected script, and a matching `script-src 'nonce-…'` CSP directive.

- [ ] **Step 2: Write failing `ErrorState` behavior test**

Render `ErrorState` and assert `role="alert"`, the supplied message, and a retry button with `type="button"`. Render `EmptyState` without a custom action and assert the default “Periksa lagi” exit exists.

- [ ] **Step 3: Run targeted tests and confirm RED**

Run:

```powershell
dotnet test backend/tests/Vokasia.Tests/Vokasia.Tests.csproj --filter "FullyQualifiedName~AccountLoginTests"
cd frontend
bun test src/components/ui/StatePrimitives.test.tsx
```

- [ ] **Step 4: Implement password reveal under CSP**

Reuse the same nonce for `<style>` and `<script>`. Add `script-src 'nonce-{nonce}'` only on the canonical login path. The button toggles `password.type`, `aria-pressed`, and its label between “Tampilkan” and “Sembunyikan”; it is at least 44 px, keyboard-focusable, and does not submit the form.

- [ ] **Step 5: Align copy and cross-reference validators**

Use this sentence on the backend login:

```text
Gunakan akun siswa, mentor, atau staf yang diberikan sekolah maupun pengelola Vokasia.
```

Add comments in `AccountEndpoints.GetSafeReturnUrl` and `frontend/src/lib/localReturnUrl.ts` pointing to the counterpart and requiring parity for security changes.

- [ ] **Step 6: Document measured verification contrast**

Compute OKLCH→sRGB→WCAG ratios for status red/green against both their status background and the white school surface. Add the measured ratios beside the frozen RAG tokens in `globals.css`; do not change token values.

- [ ] **Step 7: Implement primitive behavior and run GREEN**

Set `type="button"` on `ErrorState` retry. Run both targeted commands from Step 3.

### Task 4: Full Verification, Live Certificate QA, and Residual Audit

**Files:**
- No production files expected.
- Temporary local database row: certificate code `QA2026VALID1`, removed after browser QA.

**Interfaces:**
- Consumes: rebuilt Docker stack and one existing placement from local demo data.
- Produces: test/build evidence, live valid/invalid certificate observations, and residual gap list.

- [ ] **Step 1: Run complete frontend gates**

```powershell
cd frontend
bun test
bun run lint
bun run build
```

- [ ] **Step 2: Run complete backend tests**

Run the xUnit suite in a .NET 10 SDK verification container connected to `vokasia_default`, including the existing Testcontainers socket and MinIO/Redis overrides. Expected: zero failures; pre-existing explicit skips may remain.

- [ ] **Step 3: Rebuild and start Docker**

```powershell
docker compose up -d --build
docker compose ps
```

Wait until all seven services are healthy, then inspect worker logs to confirm readiness health evaluation and MassTransit endpoints.

- [ ] **Step 4: Run live valid-certificate QA**

Insert one reversible certificate fixture linked to an existing local placement, open `/verify/QA2026VALID1` in Chrome, inspect the visible student/school/DUDI/period result and responsive widths 320/375/414/768, then delete only that certificate row.

- [ ] **Step 5: Re-audit**

Review the final scoped diff, run the Hallmark slop/contrast/mobile gates relevant to the touched login and verification surfaces, confirm `X-Acting-Tenant` has no production use, and report only remaining verified gaps.

