#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Vokasia Dev CLI - Single entry point for test, server, db, and logs.
.DESCRIPTION
    Wraps docker compose + dotnet test + psql into one interactive menu.
    Works on Windows with Docker Desktop. No install needed - just run it.
.EXAMPLE
    .\vokasia.ps1           # interactive menu
    .\vokasia.ps1 test      # run tests directly
    .\vokasia.ps1 up        # start all infra + app
    .\vokasia.ps1 db        # open psql shell
    .\vokasia.ps1 logs api  # tail api logs
#>

param(
    [string]$Command = "",
    [string]$Arg = ""
)

$ErrorActionPreference = "Stop"
$composeFile = "docker-compose.yml"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendPath  = Join-Path $projectRoot "backend"
$sdkImage     = "mcr.microsoft.com/dotnet/sdk:10.0"

# ---------- helpers ----------
function Write-Header($text) {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor DarkCyan
    Write-Host "  ║  $text" -ForegroundColor Cyan
    Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor DarkCyan
    Write-Host ""
}

function Write-Step($text) {
    Write-Host "  → $text" -ForegroundColor Yellow
}

function Write-Ok($text) {
    Write-Host "  ✓ $text" -ForegroundColor Green
}

function Write-Err($text) {
    Write-Host "  ✗ $text" -ForegroundColor Red
}

function Invoke-Compose($argsStr) {
    Push-Location $projectRoot
    try {
        docker compose -f $composeFile @argsStr
    }
    finally {
        Pop-Location
    }
}

function Test-Docker {
    try {
        docker info *> $null
        return $true
    }
    catch {
        Write-Err "Docker is not running. Start Docker Desktop first."
        return $false
    }
}

function Get-ServiceHealth {
    # returns hashtable service -> status
    $out = docker compose -f (Join-Path $projectRoot $composeFile) ps --format "{{.Service}} {{.State}}" 2>$null
    $map = @{}
    if ($out) {
        $out | ForEach-Object {
            $parts = $_ -split ' '
            if ($parts.Count -ge 2) { $map[$parts[0]] = $parts[1] }
        }
    }
    return $map
}

# ---------- commands ----------
function Start-Servers {
    param([switch]$InfraOnly)
    if (-not (Test-Docker)) { return }
    Write-Header "STARTING SERVERS"
    if ($InfraOnly) {
        Write-Step "Bringing up infrastructure (postgres, redis, rabbitmq, minio, mailpit)..."
        Invoke-Compose "up -d postgres redis rabbitmq minio mailpit"
    }
    else {
        Write-Step "Bringing up full stack..."
        Invoke-Compose "up -d"
    }
    Write-Ok "Done. Open http://localhost:3000 (frontend), http://localhost:5000 (api), http://localhost:8025 (mailpit), http://localhost:9001 (minio)"
}

function Stop-Servers {
    Write-Header "STOPPING SERVERS"
    Write-Step "Stopping all containers..."
    Invoke-Compose "down"
    Write-Ok "Stopped."
}

function Show-Status {
    Write-Header "CONTAINER STATUS"
    Invoke-Compose "ps"
}

function Open-Db {
    if (-not (Test-Docker)) { return }
    $health = Get-ServiceHealth
    if ($health["postgres"] -ne "running") {
        Write-Err "postgres is not running. Start servers first (option 2)."
        return
    }
    Write-Header "POSTGRESQL SHELL"
    Write-Step "Connecting to vokasia database..."
    docker compose -f (Join-Path $projectRoot $composeFile) exec postgres `
        psql -U vokasia -d vokasia
}

function Tail-Logs {
    param([string]$Service = "")
    Write-Header "LOGS"
    if ($Service -eq "") {
        Write-Step "Tailing all services (Ctrl+C to stop)..."
        Invoke-Compose "logs -f"
    }
    else {
        Write-Step "Tailing $Service (Ctrl+C to stop)..."
        Invoke-Compose "logs -f $Service"
    }
}

function Run-Tests {
    param([string]$Filter = "", [switch]$LocalDotnet)
    if (-not (Test-Docker)) { return }
    Write-Header "RUNNING TESTS"

    # ensure infra is up (tests need postgres/rabbitmq/minio/redis)
    $health = Get-ServiceHealth
    $needInfra = @("postgres", "rabbitmq", "redis", "minio")
    $missing = $needInfra | Where-Object { $health[$_] -ne "running" }
    if ($missing.Count -gt 0) {
        Write-Step "Infra not fully up ($($missing -join ',')). Starting infra..."
        Invoke-Compose "up -d postgres redis rabbitmq minio"
        Write-Step "Waiting 15s for health..."
        Start-Sleep -Seconds 15
    }

    if ($LocalDotnet -and (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Step "Running dotnet test locally..."
        Push-Location $backendPath
        try { dotnet test } finally { Pop-Location }
    }
    else {
        Write-Step "Running dotnet test inside SDK container (no local SDK needed)..."
        $filterArg = if ($Filter) { "--filter `"$Filter`"" } else { "" }
        $cmd = "dotnet test $filterArg"
        docker run --rm `
            -v "${projectRoot}/backend:/src" `
            -w /src `
            $sdkImage `
            sh -c $cmd
    }
    Write-Ok "Test run finished."
}

function Show-Mailpit {
    Write-Header "MAILPIT"
    $health = Get-ServiceHealth
    if ($health["mailpit"] -ne "running") {
        Write-Step "mailpit not running, starting it..."
        Invoke-Compose "up -d mailpit"
    }
    Write-Step "Opening http://localhost:8025 ..."
    Start-Process "http://localhost:8025"
}

function Start-Dashboard {
    Write-Header "DEV DASHBOARD"
    $toolsDir = Join-Path $projectRoot "tools"
    $script = Join-Path $toolsDir "run-dashboard.ps1"
    if (-not (Test-Path $script)) {
        Write-Err "tools/run-dashboard.ps1 not found"
        return
    }
    Write-Step "Starting dashboard on http://localhost:8080 ..."
    & pwsh $script start
    Write-Ok "Dashboard: http://localhost:8080"
}

function Show-Minio {
    Write-Header "MINIO CONSOLE"
    $health = Get-ServiceHealth
    if ($health["minio"] -ne "running") {
        Write-Step "minio not running, starting it..."
        Invoke-Compose "up -d minio"
    }
    Write-Step "Opening http://localhost:9001 ..."
    Start-Process "http://localhost:9001"
}

# ---------- menu ----------
function Show-Menu {
    Write-Host ""
    Write-Host "  VOKASIA DEV CLI" -ForegroundColor Magenta
    Write-Host "  ────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "  [1] Start servers (full stack)" -ForegroundColor White
    Write-Host "  [2] Start infra only (pg/redis/rmq/minio/mailpit)" -ForegroundColor White
    Write-Host "  [3] Stop all servers" -ForegroundColor White
    Write-Host "  [4] Status (container list)" -ForegroundColor White
    Write-Host "  [5] Run tests" -ForegroundColor White
    Write-Host "  [6] Database shell (psql)" -ForegroundColor White
    Write-Host "  [7] View logs (pick service)" -ForegroundColor White
    Write-Host "  [8] Open Mailpit UI (email)" -ForegroundColor White
    Write-Host "  [9] Open MinIO console" -ForegroundColor White
    Write-Host "  [d] Start dev dashboard (web UI)" -ForegroundColor White
    Write-Host "  [q] Quit" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Choice: " -ForegroundColor Cyan -NoNewline
}

function Start-Interactive {
    while ($true) {
        Show-Menu
        $choice = Read-Host
        switch ($choice) {
            "1" { Start-Servers }
            "2" { Start-Servers -InfraOnly }
            "3" { Stop-Servers }
            "4" { Show-Status }
            "5" { Run-Tests }
            "6" { Open-Db }
            "7" {
                Write-Host "  Service (api/worker/frontend/postgres/redis/rabbitmq/minio/mailpit/caddy) or empty for all: " -ForegroundColor Cyan -NoNewline
                $svc = Read-Host
                Tail-Logs -Service $svc
            }
            "8" { Show-Mailpit }
            "9" { Show-Minio }
            "d" { Start-Dashboard }
            "q" { Write-Host "  Bye!" -ForegroundColor Green; return }
            default { Write-Err "Unknown choice: $choice" }
        }
        Write-Host ""
    }
}

# ---------- dispatch ----------
if ($Command -ne "") {
    switch ($Command) {
        "up"     { Start-Servers }
        "infra"  { Start-Servers -InfraOnly }
        "down"   { Stop-Servers }
        "status" { Show-Status }
        "test"   { Run-Tests -Filter $Arg }
        "db"     { Open-Db }
        "logs"   { Tail-Logs -Service $Arg }
        "mail"      { Show-Mailpit }
        "minio"     { Show-Minio }
        "dashboard" { Start-Dashboard }
        default     { Write-Err "Unknown command: $Command. Try: up, infra, down, status, test, db, logs, mail, minio, dashboard" }
    }
}
else {
    Start-Interactive
}
