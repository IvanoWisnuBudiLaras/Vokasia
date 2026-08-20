param([string]$ApiUrl = "http://localhost:5000", [Parameter(Mandatory)][string]$TrustedOrigin)
$ErrorActionPreference = "Stop"
$headers = @{ Origin = $TrustedOrigin; "Access-Control-Request-Method" = "POST"; "Access-Control-Request-Headers" = "content-type" }
$trusted = Invoke-WebRequest -Method Options -Uri "$ApiUrl/api/students" -Headers $headers -UseBasicParsing
if ($trusted.Headers["Access-Control-Allow-Origin"] -ne $TrustedOrigin) { throw "Trusted origin did not receive exact allow-origin header." }
$untrustedHeaders = @{ Origin = "https://untrusted.example"; "Access-Control-Request-Method" = "POST"; "Access-Control-Request-Headers" = "content-type" }
try { $untrusted = Invoke-WebRequest -Method Options -Uri "$ApiUrl/api/students" -Headers $untrustedHeaders -UseBasicParsing } catch { $untrusted = $_.Exception.Response }
if ($untrusted.Headers["Access-Control-Allow-Origin"]) { throw "Untrusted origin received allow-origin header." }
Write-Output "Trusted preflight exact; untrusted origin denied."
