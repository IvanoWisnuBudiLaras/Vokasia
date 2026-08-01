# PowerShell Test Runner dengan Output Flow & Code Coverage untuk Vokasia
Param(
    [switch]$CoverageOnly,
    [switch]$FilterFlowOnly
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$BackendDir = Resolve-Path "$ScriptDir\.."

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  VOKASIA TEST RUNNER - BATCH FLOW & COVERAGE" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

Set-Location $BackendDir

if ($FilterFlowOnly) {
    Write-Host "`n[1/2] MENJALANKAN BATCH FLOW TESTS..." -ForegroundColor Yellow
    dotnet test -c Release --filter "FullyQualifiedName~FlowTests" --logger "console;verbosity=normal"
} else {
    Write-Host "`n[1/2] MENJALANKAN SELURUH SUITE TEST (UNIT & FLOW BATCHES)..." -ForegroundColor Yellow
    dotnet test -c Release --logger "console;verbosity=normal"
}

Write-Host "`n[2/2] PEMERIKSAAN HASIL CODE COVERAGE..." -ForegroundColor Yellow
$CoverageFiles = Get-ChildItem -Path "$BackendDir\tests\Vokasia.Tests\TestResults" -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue

if ($CoverageFiles) {
    $LatestCoverage = $CoverageFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Write-Host "File Coverage Terdeteksi: $($LatestCoverage.FullName)" -ForegroundColor Green
    
    [xml]$xml = Get-Content $LatestCoverage.FullName
    $lineRate = [math]::Round([double]$xml.coverage.'line-rate' * 100, 2)
    $branchRate = [math]::Round([double]$xml.coverage.'branch-rate' * 100, 2)

    Write-Host "`n--------------------------------------------------" -ForegroundColor Cyan
    Write-Host "   RINGKASAN DATA COVERAGE UNIT TEST" -ForegroundColor Cyan
    Write-Host "--------------------------------------------------" -ForegroundColor Cyan
    Write-Host "  Line Coverage  : $lineRate %" -ForegroundColor Green
    Write-Host "  Branch Coverage: $branchRate %" -ForegroundColor Green
    Write-Host "--------------------------------------------------`n" -ForegroundColor Cyan
} else {
    Write-Host "Info: Jalankan dengan --collect:`"XPlat Code Coverage`" untuk meng-generate file coverage.cobertura.xml." -ForegroundColor Gray
}

Write-Host "Pengujian Selesai Bebas Error." -ForegroundColor Green
