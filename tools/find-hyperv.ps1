$paths = @("C:\ProgramData\Docker", "C:\Users\Public\Documents\Hyper-V")
foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Host "=== $p ==="
        Get-ChildItem -Path $p -Recurse -Filter "*.vhdx" -ErrorAction SilentlyContinue | ForEach-Object {
            $sizeGB = [math]::Round($_.Length / 1GB, 2)
            Write-Host "$($_.FullName) -> $sizeGB GB"
        }
    }
}
