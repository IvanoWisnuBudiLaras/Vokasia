$target = "C:\"
$dirs = Get-ChildItem -Path $target -Directory -Force -ErrorAction SilentlyContinue
foreach ($d in $dirs) {
    # Skip Windows and System32 to prevent long scans
    if ($d.Name -eq "Windows" -or $d.Name -eq "System Volume Information" -or $d.Name -eq "$Recycle.Bin") { continue }
    
    $size = 0
    try {
        $files = Get-ChildItem -Path $d.FullName -Recurse -File -Force -ErrorAction SilentlyContinue
        $size = ($files | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
    } catch {}
    
    if ($size -gt 100MB) {
        $sizeGB = [math]::Round($size / 1GB, 2)
        Write-Host "$($d.Name) -> $sizeGB GB"
    }
}
