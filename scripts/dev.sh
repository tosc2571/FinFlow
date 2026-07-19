#!/usr/bin/env bash
# Starts the FinFlow dev environment: API in the background, Angular dev server in the foreground.
# Ctrl+C stops both.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet run --project "$repo_root/backend/FinFlow.Api" &
api_pid=$!
trap 'kill $api_pid 2>/dev/null' EXIT

cd "$repo_root/frontend"
[ -d node_modules ] || npm install
npm start
