#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Starts the Vokasia Dev Dashboard web UI on localhost:8080 (runs in Docker).
.DESCRIPTION
    Builds and runs the vokasia-dashboard container with the host's Docker
    daemon mounted via /var/run/docker.sock (Docker-outside-of-Docker).
    The container uses project-name filters (no compose file path needed).
.EXAMPLE
    .\tools\run-dashboard.ps1 start
    .\tools\run-dashboard.ps1 stop
    .\tools\run-dashboard.ps1 restart
#>
param([string]$Action = "start")

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dashboardDir = Join-Path $scriptDir "dashboard"

function Build-Image {
    Write-Host "  Building dashboard image..." -ForegroundColor Yellow
    docker build -t vokasia-dashboard $dashboardDir
    Write-Host "  Done." -ForegroundColor Green
}

function Start-Dashboard {
    $imgExists = docker images -q vokasia-dashboard
    if (-not $imgExists) { Build-Image }
    
    $existing = docker ps --filter "name=vokasia-dashboard" --format "{{.Names}}"
    if ($existing) {
        Write-Host "  Dashboard already running at http://localhost:8080" -ForegroundColor Green
        Start-Process "http://localhost:8080"
        return
    }
    
    Write-Host "  Starting dashboard container on http://localhost:8080 ..." -ForegroundColor Yellow
    docker run -d `
        --name vokasia-dashboard `
        -p 8080:8080 `
        -v /var/run/docker.sock:/var/run/docker.sock `
        -e "COMPOSE_PROJECT=vokasia" `
        vokasia-dashboard
    
    Start-Sleep 2
    Start-Process "http://localhost:8080"
    Write-Host "  Dashboard started (PID container: $((docker inspect --format '{{.Id}}' vokasia-dashboard).Substring(0,12)))" -ForegroundColor Green
}

function Stop-Dashboard {
    docker stop vokasia-dashboard 2>$null
    docker rm vokasia-dashboard 2>$null
    Write-Host "  Dashboard stopped." -ForegroundColor Green
}

switch ($Action) {
    "build"   { Build-Image }
    "start"   { Start-Dashboard }
    "stop"    { Stop-Dashboard }
    "restart" { Stop-Dashboard; Start-Dashboard }
    default   { Write-Host "Usage: .\tools\run-dashboard.ps1 [build|start|stop|restart]" }
}