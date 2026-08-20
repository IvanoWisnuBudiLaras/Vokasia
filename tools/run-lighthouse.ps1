param([Parameter(Mandatory)][string]$OutputDirectory, [string]$BaseUrl = "", [string]$PortfolioSlug = "")
$ErrorActionPreference = "Stop"
$BaseUrl = if ($BaseUrl) { $BaseUrl } elseif ($env:PLAYWRIGHT_BASE_URL) { $env:PLAYWRIGHT_BASE_URL } else { "http://localhost:3000" }
$PortfolioSlug = if ($PortfolioSlug) { $PortfolioSlug } elseif ($env:LIGHTHOUSE_PORTFOLIO_SLUG) { $env:LIGHTHOUSE_PORTFOLIO_SLUG } else { "demo" }
$null = New-Item -ItemType Directory -Force $OutputDirectory
$routes = @("student", "p/$PortfolioSlug")
foreach ($route in $routes) {
  $safe = $route.Replace('/', '-'); $json = Join-Path $OutputDirectory "$safe.json"
  lighthouse "$BaseUrl/$route" --only-categories=performance,accessibility --form-factor=mobile --output=json --output-path="$json" --chrome-flags="--headless=new"
  lighthouse "$BaseUrl/$route" --only-categories=performance,accessibility --form-factor=mobile --output=html --output-path="$(Join-Path $OutputDirectory "$safe.html")" --chrome-flags="--headless=new"
  $report = Get-Content -Raw $json | ConvertFrom-Json
  $performance = [math]::Round($report.categories.performance.score * 100, 2); $accessibility = [math]::Round($report.categories.accessibility.score * 100, 2)
  if ($performance -lt 85 -or $accessibility -lt 90) { throw "$route failed Lighthouse thresholds: performance=$performance accessibility=$accessibility" }
  [ordered]@{ route = "/$route"; performance = $performance; accessibility = $accessibility; thresholds = @{ performance = 85; accessibility = 90 } } | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory "$safe-summary.json")
}
