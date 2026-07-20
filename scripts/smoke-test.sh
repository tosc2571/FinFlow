#!/usr/bin/env bash
# End-to-end smoke test against a built FinFlow app.
# Usage: smoke-test.sh <app-dir>   (dir containing FinFlow.Api[.exe] or FinFlow.Api.dll)
# Starts the app on http://localhost:5199 with a fresh database, asserts
# SPA hosting + import + dedupe + dashboard + export, restarts it to verify
# the daily backup fires on a startup that finds an existing database, then stops it.
# Requires: curl, jq. Used by .github/workflows/{smoke-ci,release}.yml.
set -euo pipefail

app_dir="$(cd "$1" && pwd)"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$app_dir"
# Explicit, isolated DB location — the published app defaults to a stable per-user data
# directory outside the repo (see AppDataDirectory / issue #4), which this override
# deliberately bypasses so the smoke test never touches a real local FinFlow install.
db_path="$app_dir/finflow-smoketest.db"
rm -f "$db_path" "$db_path-shm" "$db_path-wal"
rm -rf "$app_dir/backups"
export ConnectionStrings__Default="Data Source=$db_path"

start_app() {
  if [ -f FinFlow.Api ]; then
    chmod +x FinFlow.Api
    ./FinFlow.Api &
  elif [ -f FinFlow.Api.exe ]; then
    ./FinFlow.Api.exe &
  else
    dotnet FinFlow.Api.dll &
  fi
  app_pid=$!
  for _ in $(seq 1 30); do
    curl -sf http://localhost:5199/api/import/batches > /dev/null && break
    sleep 1
  done
}

stop_app() {
  kill "$app_pid" 2>/dev/null || true
  wait "$app_pid" 2>/dev/null || true
}
trap stop_app EXIT

start_app

# Fresh install: no backup yet, and none should be created on this very first startup
# (there's nothing to back up) — see DatabaseBackupTests for the unit-level coverage.
test ! -d "$app_dir/backups"

# SPA served at /, client route falls back to index.html, favicon present
curl -sf http://localhost:5199/ | grep -q '<app-root>'
test "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5199/dashboard)" = 200
curl -sf http://localhost:5199/favicon.svg > /dev/null

# Import both synthetic samples: 4 + 4 bookings
curl -sf -X POST http://localhost:5199/api/import/ \
  -F files=@"$repo_root/samples/dkb-sample.csv" -F files=@"$repo_root/samples/ing-sample.csv" \
  | jq -e 'map(.imported) | add == 8' > /dev/null

# Re-import: everything must be recognized as duplicate
curl -sf -X POST http://localhost:5199/api/import/ -F files=@"$repo_root/samples/dkb-sample.csv" \
  | jq -e '.[0].imported == 0 and .[0].duplicates == 4' > /dev/null

# Dashboard aggregates and XLSX export work
curl -sf 'http://localhost:5199/api/dashboard/summary?year=2025' \
  | jq -e '.transactionCount == 8' > /dev/null
test "$(curl -s -o /dev/null -w '%{size_download}' 'http://localhost:5199/api/export/xlsx?year=2025')" -gt 5000

# Restart: the database now exists, so this startup must produce today's dated backup.
stop_app
start_app
test -f "$app_dir/backups/finflow-$(date -u +%F).db"

echo "Smoke test passed."
