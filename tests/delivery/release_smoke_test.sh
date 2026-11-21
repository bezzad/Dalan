#!/usr/bin/env bash
# Rules: DEL-03, CLI-02
# Runs the published self-contained release with no .NET on PATH and checks /health.
# Usage: tests/delivery/release_smoke_test.sh <publish-folder>
set -euo pipefail
dir="$1"
fail() { echo "FAIL: $*"; exit 1; }

files=$(ls "$dir" | LC_ALL=C sort | tr '\n' ' ')
[ "$files" = "Dalan.Host appsettings.json " ] || fail "release has extra files: $files"

# CLI-02: the PWA page is inside the executable, not beside it.
grep -a -q '<html lang="fa" dir="rtl">' "$dir/Dalan.Host" || fail "the client page is not inside the executable"

# Hide any installed .NET from the release.
env -i PATH=/usr/bin:/bin HOME="$HOME" DOTNET_ROOT=/nonexistent ASPNETCORE_URLS=http://127.0.0.1:18090 \
  bash -c "cd '$dir' && exec ./Dalan.Host" >/dev/null 2>&1 & pid=$!
trap 'kill $pid 2>/dev/null || true' EXIT

for _ in $(seq 50); do curl -fs -o /dev/null http://127.0.0.1:18090/health && break; sleep 0.2; done
curl -fs http://127.0.0.1:18090/health | grep -q '"status":"ok"' || fail "/health did not answer ok"
echo "PASS DEL-03 CLI-02 self-contained release runs and holds the client"
