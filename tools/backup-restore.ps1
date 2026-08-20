param([Parameter(Mandatory)][string]$EvidenceDirectory, [switch]$VerifyRestore)
$ErrorActionPreference = "Stop"
$null = New-Item -ItemType Directory -Force $EvidenceDirectory
$db = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { "vokasia" }; $user = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { "vokasia" }; $restoreDb = "vokasia_restore_verify"
$backup = Join-Path $EvidenceDirectory "vokasia.sql"
docker compose exec -T postgres pg_dump -U $user -d $db --format=plain | Set-Content -Encoding utf8 $backup
if (-not (Test-Path $backup) -or (Get-Item $backup).Length -eq 0) { throw "Backup file is empty." }
if ($VerifyRestore) {
  $tables = @("Tenants", "AspNetUsers", "Students", "Placements", "JournalEntries", "Assessments", "OutboxMessages")
  $sourceCounts = [ordered]@{}
  foreach ($table in $tables) { $identifier = '"' + $table + '"'; $sourceCounts[$table] = ((docker compose exec -T postgres psql -U $user -d $db -Atc ("SELECT count(*) FROM {0};" -f $identifier)) -join "").Trim() }
  $sourceCounts | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory "source-counts.json")
  docker compose exec -T postgres psql -U $user -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$restoreDb';" | Out-Null
  docker compose exec -T postgres psql -U $user -d postgres -c "DROP DATABASE IF EXISTS $restoreDb;" | Out-Null
  docker compose exec -T postgres psql -U $user -d postgres -c "CREATE DATABASE $restoreDb;" | Out-Null
  Get-Content -Raw $backup | docker compose exec -T postgres psql -U $user -d $restoreDb | Out-File (Join-Path $EvidenceDirectory "restore.log")
  $counts = [ordered]@{}
  foreach ($table in $tables) { $identifier = '"' + $table + '"'; $counts[$table] = ((docker compose exec -T postgres psql -U $user -d $restoreDb -Atc ("SELECT count(*) FROM {0};" -f $identifier)) -join "").Trim() }
  $counts | ConvertTo-Json | Set-Content (Join-Path $EvidenceDirectory "restored-counts.json")
  if (($counts.Values | Where-Object { $_ -eq "0" }).Count -gt 0) { throw "A critical restored table has zero rows." }
  foreach ($table in $tables) { if ($sourceCounts[$table] -ne $counts[$table]) { throw "Restore count mismatch for ${table}: source=$($sourceCounts[$table]) restored=$($counts[$table])" } }
}
Write-Output "Backup and restore verification completed: $EvidenceDirectory"
