# Starts the FinFlow dev environment: API in a new window, Angular dev server in this one.
# Stop: Ctrl+C here (frontend), close the API window separately.
$repoRoot = Split-Path -Parent $PSScriptRoot

Start-Process dotnet -ArgumentList 'run', '--project', (Join-Path $repoRoot 'backend/FinFlow.Api')

Set-Location (Join-Path $repoRoot 'frontend')
if (-not (Test-Path 'node_modules')) { npm install }
npm start
