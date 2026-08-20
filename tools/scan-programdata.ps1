$target = "C:\ProgramData"
$dirs = Get-ChildItem -Path $target -Directory -Force -ErrorAction SilentlyContinue
foreach ($d in $dirs) {
    $size = 0
    try {
        $files = Get-ChildItem -Path $d.FullName -Recurse -File -Force -ErrorAction SilentlyContinue
        $size = ($files | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
    } catch {}
    if ($size -gt 500MB) {
        $sizeGB = [math]::Round($size / 1GB, 2)
        Write-Host "$($d.Name) -> $sizeGB GB"
    }
}