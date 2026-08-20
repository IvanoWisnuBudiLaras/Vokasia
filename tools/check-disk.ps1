$dirs = Get-ChildItem -Path C:\ -Directory -Force -ErrorAction SilentlyContinue
$results = @()
foreach ($d in $dirs) {
    $size = (Get-ChildItem -Path $d.FullName -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
    $results += [PSCustomObject]@{Dir=$d.Name; SizeGB=[math]::Round($size/1GB,1)}
}
$results | Sort-Object SizeGB -Descending | Select-Object -First 15 | Format-Table -AutoSize