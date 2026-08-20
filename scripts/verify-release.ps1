[CmdletBinding()]
param(
  [switch]$CleanState,
  [switch]$AllowDirty,
  [switch]$SkipLoad,
  [switch]$SkipLighthouse,
  [switch]$SkipSecurity,
  [switch]$SkipRestore,
  [int]$ReadinessTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$evidence = Join-Path $repo "artifacts/release/$stamp"
$null = New-Item -ItemType Directory -Force -Path $evidence, "$evidence/backend-tests", "$evidence/frontend", "$evidence/playwright", "$evidence/load", "$evidence/lighthouse", "$evidence/bundle", "$evidence/backup", "$evidence/security"
$results = [ordered]@{}
$requiredFailure = $false

function Set-Result([string]$Name, [string]$Status, [string]$Detail = "") { $results[$Name] = [ordered]@{ status = $Status; detail = $Detail } }
function Has-Command([string]$Name) { return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue) }
function Run-Gate([string]$Name, [scriptblock]$Action, [string]$Log, [switch]$Required) {
  try {
    $global:LASTEXITCODE = 0
    & $Action *> $Log
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "exit code $LASTEXITCODE" }
    Set-Result $Name "PASS" $Log
  } catch {
    Set-Result $Name "FAIL" "$($_.Exception.Message); log=$Log"
    if ($Required) { $script:requiredFailure = $true }
  }
}
function Skip-Gate([string]$Name, [string]$Reason, [switch]$Required) {
  Set-Result $Name "SKIPPED" $Reason
  if ($Required) { $script:requiredFailure = $true }
}
function Read-Configured([string]$Name) {
  $processValue = [Environment]::GetEnvironmentVariable($Name)
  if ($processValue) { return $processValue }
  $envFile = Join-Path $repo ".env"
  if (Test-Path $envFile) {
    $line = Get-Content $envFile | Where-Object { $_ -match "^$([regex]::Escape($Name))=" } | Select-Object -First 1
    if ($line) { return ($line -split "=", 2)[1] }
  }
  return $null
}
function Wait-Http([string]$Uri, [string]$Name) {
  $deadline = (Get-Date).AddSeconds($ReadinessTimeoutSeconds)
  do {
    try { $response = Invoke-WebRequest -Uri $Uri -TimeoutSec 5; if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) { return } } catch { }
    Start-Sleep -Seconds 2
  } while ((Get-Date) -lt $deadline)
  throw "$Name readiness timeout: $Uri"
}
function Wait-ComposeHealth([string[]]$Services) {
  $deadline = (Get-Date).AddSeconds($ReadinessTimeoutSeconds)
  do {
    $ready = $true
    foreach ($service in $Services) {
      $id = (& docker compose ps -q $service).Trim()
      if (-not $id) { $ready = $false; continue }
      $state = (& docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $id).Trim()
      if ($state -notin @("healthy", "running")) { $ready = $false }
    }
    if ($ready) { return }
    Start-Sleep -Seconds 2
  } while ((Get-Date) -lt $deadline)
  throw "Compose readiness timeout for: $($Services -join ', ')"
}

$gitSha = (& git -C $repo rev-parse HEAD 2>$null).Trim()
$dirty = [bool]((& git -C $repo status --porcelain 2>$null).Trim())
Set-Result "working_tree" ($(if ($dirty) { "DIRTY" } else { "CLEAN" })) "sha=$gitSha"
if ($dirty -and -not $AllowDirty) { Set-Result "dirty_tree_policy" "FAIL" "Working tree is dirty; rerun with -AllowDirty."; $requiredFailure = $true; throw "Release verification stopped before destructive work." }

foreach ($tool in @("dotnet", "bun", "docker")) { if (-not (Has-Command $tool)) { Set-Result "preflight_$tool" "FAIL" "Required executable missing: $tool"; $requiredFailure = $true } else { Set-Result "preflight_$tool" "PASS" } }
if (Has-Command "docker") {
  docker compose version *> $null
  if ($LASTEXITCODE -eq 0) { Set-Result "preflight_docker_compose" "PASS" } else { Set-Result "preflight_docker_compose" "FAIL" "docker compose version exit code $LASTEXITCODE"; $requiredFailure = $true }
}
if (Has-Command "bun") { Run-Gate "preflight_playwright" { Push-Location "$repo/frontend"; try { bunx playwright --version } finally { Pop-Location } } "$evidence/preflight-playwright.log" -Required }
if (-not $SkipLoad -and -not (Has-Command "k6")) { Set-Result "preflight_k6" "FAIL" "Install k6 or rerun explicitly with -SkipLoad."; $requiredFailure = $true }
if (-not $SkipLighthouse -and -not (Has-Command "lighthouse")) { Set-Result "preflight_lighthouse" "FAIL" "Install Lighthouse CLI or rerun explicitly with -SkipLighthouse."; $requiredFailure = $true }
if (-not $SkipSecurity -and -not (Has-Command "trivy")) { Set-Result "preflight_trivy" "FAIL" "Install Trivy or rerun explicitly with -SkipSecurity."; $requiredFailure = $true }
foreach ($key in @("NEXT_PUBLIC_APP_URL", "API_PUBLIC_URL", "API_INTERNAL_URL", "Cors__AllowedOrigins__0", "MINIO_PUBLIC_URL")) { if (Read-Configured $key) { Set-Result "config_$key" "PASS" } else { Set-Result "config_$key" "FAIL" "Missing configuration (value not printed)."; $requiredFailure = $true } }
if ($requiredFailure) { $results | ConvertTo-Json -Depth 6 | Set-Content "$evidence/summary.json"; throw "Preflight failed. Evidence: $evidence" }

Run-Gate "backend_restore" { dotnet restore "$repo/backend/Vokasia.sln" } "$evidence/backend-tests/restore.log" -Required
Run-Gate "backend_build" { dotnet build "$repo/backend/Vokasia.sln" --no-restore --configuration Release } "$evidence/backend-tests/build.log" -Required
Run-Gate "backend_tests" { dotnet test "$repo/backend/Vokasia.sln" --no-build --configuration Release --logger "trx;LogFileName=release.trx" --results-directory "$evidence/backend-tests" } "$evidence/backend-tests/test.log" -Required
Run-Gate "frontend_install" { Push-Location "$repo/frontend"; try { bun install --frozen-lockfile } finally { Pop-Location } } "$evidence/frontend/install.log" -Required
Run-Gate "frontend_lint" { Push-Location "$repo/frontend"; try { bun run lint } finally { Pop-Location } } "$evidence/frontend/lint.log" -Required
Run-Gate "frontend_tests" { Push-Location "$repo/frontend"; try { bun run test:unit } finally { Pop-Location } } "$evidence/frontend/test.log" -Required
Run-Gate "frontend_build" { Push-Location "$repo/frontend"; try { bun run build } finally { Pop-Location } } "$evidence/frontend/build.log" -Required

if ($CleanState) { Run-Gate "compose_clean_state" { docker compose down --volumes --remove-orphans } "$evidence/compose-clean.log" -Required }
Run-Gate "compose_up" { docker compose up -d --build } "$evidence/compose-up.log" -Required
Run-Gate "compose_readiness" { Wait-ComposeHealth @("postgres", "redis", "rabbitmq", "minio", "api", "worker", "frontend") } "$evidence/readiness.log" -Required
Run-Gate "seed" { docker compose exec -T api dotnet Vokasia.Api.dll seed demo } "$evidence/seed.log" -Required
Run-Gate "api_health" { Wait-Http "http://localhost:5000/health" "API" } "$evidence/api-health.log" -Required
Run-Gate "frontend_health" { Wait-Http "http://localhost:3000" "frontend" } "$evidence/frontend-health.log" -Required
Run-Gate "cors_check" { & "$repo/scripts/check-cors.ps1" -ApiUrl "http://localhost:5000" -TrustedOrigin (Read-Configured "NEXT_PUBLIC_APP_URL") } "$evidence/cors.log" -Required
Run-Gate "playwright" {
  Push-Location "$repo/frontend"
  $previousReportDir = $env:PLAYWRIGHT_HTML_OUTPUT_DIR
  try { $env:PLAYWRIGHT_HTML_OUTPUT_DIR = "$evidence/playwright/report"; bun run test:e2e }
  finally { if ($null -eq $previousReportDir) { Remove-Item Env:PLAYWRIGHT_HTML_OUTPUT_DIR -ErrorAction SilentlyContinue } else { $env:PLAYWRIGHT_HTML_OUTPUT_DIR = $previousReportDir }; Pop-Location }
} "$evidence/playwright/run.log" -Required

if ($SkipLoad) { Skip-Gate "load" "Explicit -SkipLoad" -Required } else { Run-Gate "load" { k6 run --out "json=$evidence/load/results.json" "$repo/tools/load-test.js" } "$evidence/load/run.log" -Required }
if ($SkipLighthouse) { Skip-Gate "lighthouse" "Explicit -SkipLighthouse" -Required } else { Run-Gate "lighthouse" { & "$repo/tools/run-lighthouse.ps1" -OutputDirectory "$evidence/lighthouse" } "$evidence/lighthouse/run.log" -Required }
Run-Gate "bundle" { & "$repo/tools/measure-bundle.ps1" -OutputFile "$evidence/bundle/summary.json" } "$evidence/bundle/run.log" -Required
if ($SkipRestore) { Skip-Gate "backup_restore" "Explicit -SkipRestore" -Required } else { Run-Gate "backup_restore" { & "$repo/tools/backup-restore.ps1" -EvidenceDirectory "$evidence/backup" -VerifyRestore } "$evidence/backup/run.log" -Required }
if ($SkipSecurity) { Skip-Gate "security" "Explicit -SkipSecurity" -Required } else {
  Run-Gate "security" {
    dotnet list "$repo/backend/Vokasia.sln" package --vulnerable --include-transitive
    if ($LASTEXITCODE -ne 0) { throw "dotnet vulnerability audit exit code $LASTEXITCODE" }
    bun audit --json
    if ($LASTEXITCODE -ne 0) { throw "bun audit exit code $LASTEXITCODE" }
    trivy fs --exit-code 1 --severity HIGH,CRITICAL "$repo"
    if ($LASTEXITCODE -ne 0) { throw "trivy exit code $LASTEXITCODE" }
  } "$evidence/security/run.log" -Required
}

$summary = [ordered]@{ timestamp = (Get-Date).ToUniversalTime().ToString("o"); commit = $gitSha; dirty = $dirty; evidence = $evidence; gates = $results }
$summary | ConvertTo-Json -Depth 8 | Set-Content "$evidence/summary.json"
$markdown = @("# Release verification", "", "- Timestamp: $($summary.timestamp)", "- Commit: $gitSha", "- Working tree: $(if ($dirty) { 'DIRTY' } else { 'CLEAN' })", "- Evidence: $evidence", "", "| Gate | Status | Detail |", "| --- | --- | --- |")
foreach ($entry in $results.GetEnumerator()) { $markdown += "| $($entry.Key) | $($entry.Value.status) | $($entry.Value.detail -replace '\|', '\\|') |" }
$markdown | Set-Content "$evidence/summary.md"
if ($requiredFailure -or ($results.Values | Where-Object { $_.status -in @("FAIL", "SKIPPED", "NOT_RUN") })) { Write-Error "Release verification FAILED or incomplete. Evidence: $evidence"; exit 1 }
Write-Host "Release verification completed. Evidence: $evidence"; exit 0
