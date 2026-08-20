param([string]$BaseUrl = "http://localhost:5000", [int]$Rate = 50, [int]$DurationSeconds = 300)
Write-Output "Load-test contract: $Rate req/s for $DurationSeconds seconds against $BaseUrl"
Write-Output "NOT EXECUTED: provide authenticated journal fixture and run with a production-like load runner."
