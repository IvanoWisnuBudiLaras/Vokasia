param([Parameter(Mandatory)][string]$OutputFile, [int]$ThresholdBytes = 204800)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path; $manifest = Join-Path $repo "frontend/.next/app-build-manifest.json"
if (-not (Test-Path $manifest)) { throw "Missing frontend/.next/app-build-manifest.json; run bun run build first." }
$assets = Get-ChildItem (Join-Path $repo "frontend/.next/static") -Recurse -File
$bytes = ($assets | Measure-Object -Property Length -Sum).Sum
$result = [ordered]@{ metric = "all Next static assets emitted by build"; route = "/student"; bytes = $bytes; thresholdBytes = $ThresholdBytes; exactRouteInitialPayload = $false; note = "Next app manifest is inspected for existence; static asset total is an upper-bound proxy, not a claim about exact browser initial payload."; status = $(if ($bytes -lt $ThresholdBytes) { "PASS" } else { "FAIL" }) }
$result | ConvertTo-Json | Set-Content $OutputFile
if ($result.status -eq "FAIL") { throw "Bundle upper-bound exceeds threshold: $bytes bytes" }
