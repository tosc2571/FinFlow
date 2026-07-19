#!/usr/bin/env bash
# Builds the whole app into a single, locally runnable package (no Docker, no Node at runtime):
# Angular production build -> copied into the API's wwwroot -> dotnet publish.
# Result: publish/ — run `dotnet publish/FinFlow.Api.dll` and open http://localhost:5199.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$repo_root/frontend"
[ -d node_modules ] || npm ci
npx ng build
cd "$repo_root"

wwwroot="$repo_root/backend/FinFlow.Api/wwwroot"
rm -rf "$wwwroot"
cp -r "$repo_root/frontend/dist/finflow/browser" "$wwwroot"

dotnet publish "$repo_root/backend/FinFlow.Api/FinFlow.Api.csproj" -c Release -o "$repo_root/publish"

echo ""
echo "Done. Start FinFlow with:  dotnet $repo_root/publish/FinFlow.Api.dll"
echo "Then open http://localhost:5199 in your browser."
