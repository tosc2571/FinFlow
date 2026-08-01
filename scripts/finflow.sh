#!/usr/bin/env bash
# FinFlow launcher. Run this any time you want to use FinFlow:
#   - No local install yet, or a newer release exists -> downloads it, verifies its
#     SHA256 checksum, and installs it into ./app (existing data is untouched - the
#     database lives outside ./app, see README).
#   - Already up to date -> just starts the app.
#   - Already running -> just opens the browser.
# Fully automatic, no prompts. Safe to re-run any time.
#
# GitHub's release JSON is parsed with grep/sed rather than jq, deliberately -
# curl/tar/sha256sum are near-universal, jq is not, and the two fields we need
# (tag_name, browser_download_url) have a stable enough shape for this.
set -euo pipefail

repo="tosc2571/FinFlow"
launcher_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
app_dir="$launcher_dir/app"
version_file="$app_dir/VERSION"
health_url="http://localhost:5199/api/import/batches"

is_running() {
  curl -sf "$health_url" > /dev/null 2>&1
}

local_version=""
[ -f "$version_file" ] && local_version="$(cat "$version_file")"

release_json="$(curl -sf -H "User-Agent: FinFlow-Launcher" "https://api.github.com/repos/$repo/releases/latest" 2>/dev/null || true)"
latest_tag=""
if [ -n "$release_json" ]; then
  latest_tag="$(echo "$release_json" | grep -o '"tag_name" *: *"[^"]*"' | head -1 | sed -E 's/.*"([^"]+)"$/\1/')"
fi

if [ -z "$release_json" ]; then
  if [ -z "$local_version" ]; then
    echo "No local FinFlow install found and no release is available - nothing to start." >&2
    exit 1
  fi
  echo "Warning: could not check for updates (GitHub unreachable, or no release published yet) - starting the existing install." >&2
elif [ "$latest_tag" != "$local_version" ]; then
  asset_name="finflow-${latest_tag}-linux-x64.tar.gz"
  asset_url="$(echo "$release_json" | grep -o "\"browser_download_url\" *: *\"[^\"]*${asset_name}\"" | sed -E 's/.*"(https[^"]+)"/\1/')"
  checksum_url="$(echo "$release_json" | grep -o "\"browser_download_url\" *: *\"[^\"]*${asset_name}\.sha256\"" | sed -E 's/.*"(https[^"]+)"/\1/')"
  if [ -z "$asset_url" ] || [ -z "$checksum_url" ]; then
    echo "Release $latest_tag is missing the expected linux-x64 assets ($asset_name[.sha256])." >&2
    exit 1
  fi

  echo "Downloading FinFlow $latest_tag ..."
  tmp_dir="$(mktemp -d)"
  trap 'rm -rf "$tmp_dir"' EXIT
  curl -sfL -o "$tmp_dir/finflow.tar.gz" "$asset_url"
  expected_hash="$(curl -sfL "$checksum_url" | tr -d '[:space:]' | tr '[:upper:]' '[:lower:]')"
  actual_hash="$(sha256sum "$tmp_dir/finflow.tar.gz" | cut -d' ' -f1)"
  if [ "$actual_hash" != "$expected_hash" ]; then
    echo "Checksum mismatch for $latest_tag (expected $expected_hash, got $actual_hash). Update aborted - existing install untouched." >&2
    exit 1
  fi
  echo "Checksum verified."

  # Extract into a staging folder first and only swap it into place once we know
  # it's a valid FinFlow build - minimizes the window where ./app could end up
  # half-replaced if the download or extraction failed partway.
  staging_dir="$tmp_dir/staged"
  mkdir -p "$staging_dir"
  tar xzf "$tmp_dir/finflow.tar.gz" -C "$staging_dir"
  if [ ! -f "$staging_dir/FinFlow.Api" ]; then
    echo "Downloaded archive for $latest_tag doesn't look like a valid FinFlow release (FinFlow.Api missing)." >&2
    exit 1
  fi

  echo "Installing ..."
  rm -rf "$app_dir"
  mv "$staging_dir" "$app_dir"
  echo "FinFlow updated to $latest_tag."
else
  echo "FinFlow is up to date ($local_version)."
fi

if [ ! -f "$app_dir/FinFlow.Api" ]; then
  echo "FinFlow is not installed and no update could be downloaded." >&2
  exit 1
fi

if is_running; then
  echo "FinFlow is already running."
else
  echo "Starting FinFlow ..."
  chmod +x "$app_dir/FinFlow.Api"
  (cd "$app_dir" && nohup ./FinFlow.Api > /dev/null 2>&1 &)
  ready=false
  for _ in $(seq 1 30); do
    if is_running; then ready=true; break; fi
    sleep 1
  done
  if [ "$ready" != true ]; then
    echo "Warning: FinFlow didn't respond in time - open http://localhost:5199 manually once it's up." >&2
  fi
fi

url="http://localhost:5199"
if command -v xdg-open > /dev/null 2>&1; then
  xdg-open "$url" > /dev/null 2>&1 &
elif command -v open > /dev/null 2>&1; then
  open "$url"
else
  echo "Open $url in your browser."
fi
