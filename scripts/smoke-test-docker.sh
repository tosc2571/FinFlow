#!/usr/bin/env bash
# End-to-end smoke test against the Docker image (mirrors scripts/smoke-test.sh, but exercises
# the container specifically: image builds, the app starts and migrates the DB on a mounted
# volume, serves real traffic, and — critically — the data survives a container restart against
# the same volume, which is the whole point of persisting to a volume in the first place.
# Usage: smoke-test-docker.sh <image-tag>
# Requires: docker, curl, jq.
set -euo pipefail

image="$1"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
data_dir="$(mktemp -d)"
container=finflow-smoke-test

cleanup() {
  docker rm -f "$container" > /dev/null 2>&1 || true
  # The container runs as root, so files it wrote under $data_dir are root-owned on the host
  # too — harmless on a throwaway CI runner, but don't let a permission-denied rm here mask an
  # otherwise-passing test run.
  rm -rf "$data_dir" 2>/dev/null || true
}
trap cleanup EXIT

start_container() {
  docker run -d --name "$container" -p 5199:5199 -v "$data_dir:/data" "$image" > /dev/null
  for _ in $(seq 1 30); do
    curl -sf http://localhost:5199/api/import/batches > /dev/null && return
    sleep 1
  done
  echo "Container did not become ready in time. Logs:" >&2
  docker logs "$container" >&2
  exit 1
}

stop_container() {
  docker stop "$container" > /dev/null
  docker rm "$container" > /dev/null
}

start_container

# SPA served at /, client route falls back to index.html
curl -sf http://localhost:5199/ | grep -q '<app-root>'
test "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:5199/dashboard)" = 200

# Import both synthetic samples: 4 + 4 bookings
curl -sf -X POST http://localhost:5199/api/import/ \
  -F files=@"$repo_root/samples/dkb-sample.csv" -F files=@"$repo_root/samples/ing-sample.csv" \
  | jq -e 'map(.imported) | add == 8' > /dev/null

curl -sf 'http://localhost:5199/api/dashboard/summary?year=2025' \
  | jq -e '.transactionCount == 8' > /dev/null
test "$(curl -s -o /dev/null -w '%{size_download}' 'http://localhost:5199/api/export/xlsx?year=2025')" -gt 5000

# The DB must have landed on the mounted volume, not somewhere inside the (about to be
# discarded) container filesystem. No backup yet — nothing to back up on a first startup.
test -f "$data_dir/finflow.db"
test ! -d "$data_dir/backups"

# Restart against the SAME volume: the 8 imported transactions must still be there — this is
# the actual persistence guarantee the volume mount exists to provide. This startup also finds
# an existing DB, so today's dated backup must appear now.
stop_container
start_container
curl -sf 'http://localhost:5199/api/dashboard/summary?year=2025' \
  | jq -e '.transactionCount == 8' > /dev/null
test -f "$data_dir/backups/finflow-$(date -u +%F).db"

echo "Docker smoke test passed."
