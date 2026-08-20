$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  RELOCATING DOCKER WSL DATA TO DRIVE I" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Ensure Target Directory
$targetDir = "I:\DockerWSL"
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir | Out-Null
    Write-Host "  Created $targetDir" -ForegroundColor Green
}

# Stop Docker Service
Write-Host "  Stopping Docker Desktop service..." -ForegroundColor Yellow
Stop-Service -Name "com.docker.service" -Force -ErrorAction SilentlyContinue
Get-Process -Name "Docker Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
wsl --shutdown

Write-Host "  Docker stopped." -ForegroundColor Green
Start-Sleep -Seconds 5

# Exporting wsl data
Write-Host "  Exporting docker-desktop to I:\DockerWSL\data.tar (This may take a few minutes)..." -ForegroundColor Yellow
& wsl --export docker-desktop "I:\DockerWSL\data.tar"
Write-Host "  Export complete." -ForegroundColor Green

# Unregister old distro
Write-Host "  Unregistering old docker-desktop distro..." -ForegroundColor Yellow
& wsl --unregister docker-desktop
Write-Host "  Unregistered." -ForegroundColor Green

# Import to new location
Write-Host "  Importing docker-desktop to I:\DockerWSL\data\..." -ForegroundColor Yellow
$importPath = "I:\DockerWSL\data"
if (-not (Test-Path $importPath)) {
    New-Item -ItemType Directory -Path $importPath | Out-Null
}
& wsl --import docker-desktop $importPath "I:\DockerWSL\data.tar" --version 2
Write-Host "  Import complete." -ForegroundColor Green

# Clean up tar file
Write-Host "  Removing temporary data.tar..." -ForegroundColor Yellow
Remove-Item "I:\DockerWSL\data.tar" -Force
Write-Host "  Cleanup complete." -ForegroundColor Green

# Start Docker Service
Write-Host "  Starting Docker Desktop service..." -ForegroundColor Yellow
Start-Service -Name "com.docker.service"
Write-Host "  Done." -ForegroundColor Green
