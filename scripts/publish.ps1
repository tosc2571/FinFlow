# Builds the whole app into a single, locally runnable package (no Docker, no Node at runtime):
# Angular production build -> copied into the API's wwwroot -> dotnet publish.
# Result: publish/FinFlow.Api.exe — run it and open http://localhost:5199.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location (Join-Path $repoRoot 'frontend')
if (-not (Test-Path 'node_modules')) { npm ci }
npx ng build
Pop-Location

$wwwroot = Join-Path $repoRoot 'backend/FinFlow.Api/wwwroot'
if (Test-Path $wwwroot) { Remove-Item -Recurse -Force $wwwroot }
Copy-Item (Join-Path $repoRoot 'frontend/dist/finflow/browser') $wwwroot -Recurse

$out = Join-Path $repoRoot 'publish'
dotnet publish (Join-Path $repoRoot 'backend/FinFlow.Api/FinFlow.Api.csproj') -c Release -o $out

Write-Host ''
Write-Host "Done. Start FinFlow with:  $out\FinFlow.Api.exe"
Write-Host 'Then open http://localhost:5199 in your browser.'
