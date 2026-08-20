# 1. Stop Docker Desktop
Write-Host "Stopping Docker..." -ForegroundColor Yellow
Get-Process -Name "Docker Desktop" -ErrorAction SilentlyContinue | Stop-Process -Force
Stop-Service -Name "com.docker.service" -Force -ErrorAction SilentlyContinue
wsl --shutdown

# 2. Enable WSL2 backend in Docker settings
Write-Host "Enabling WSL2 backend..." -ForegroundColor Yellow
$settingsPath = "$env:APPDATA\Docker\settings.json"
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$settings.wslEngineEnabled = $true
$settings | ConvertTo-Json -Compress | Set-Content $settingsPath

# 3. Move docker_data.vhdx to I: drive if it exists
$vhdxPath = "$env:LOCALAPPDATA\Docker\wsl\disk\docker_data.vhdx"
$targetDir = "I:\DockerWSL"
if (Test-Path $vhdxPath) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    $targetVhdx = "$targetDir\docker_data.vhdx"
    Write-Host "Moving $vhdxPath to $targetVhdx ..." -ForegroundColor Yellow
    Move-Item -Path $vhdxPath -Destination $targetVhdx -Force
    # Create directory junction so Docker finds it
    New-Item -ItemType Junction -Path "$env:LOCALAPPDATA\Docker\wsl\disk" -Target $targetDir -Force | Out-Null
}

# 4. Clean Docker build cache and old images
Write-Host "Cleaning Docker cache..." -ForegroundColor Yellow
docker system prune -a -f 2>$null
docker builder prune -a -f 2>$null

# 5. Start Docker Desktop
Write-Host "Starting Docker Desktop..." -ForegroundColor Yellow
Start-Process "$env:LOCALAPPDATA\Programs\DockerDesktop\Docker Desktop.exe"
Start-Sleep 15

Write-Host "Done. Docker should be running with WSL2 backend." -ForegroundColor Green