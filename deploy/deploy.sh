#!/usr/bin/env bash
# Deploy one release to the server and roll back if it is not ready (DEL-04, DEL-05, DEL-06).
# CI pushes over SSH; the server never pulls.
#
# Usage: deploy/deploy.sh <release.tar.gz> <version>
#
# Settings (environment):
#   DEPLOY_HOST          user@host, or "local" to run on this machine (used by tests)
#   DEPLOY_DIR           default /opt/dalan
#   KEEP_RELEASES        default 5
#   READY_WAIT_SECONDS   default 30
#   READY_URL            default http://127.0.0.1:5000/ready
#   SYSTEMCTL            default "sudo systemctl"
set -euo pipefail

archive="$1"
version="$2"
host="${DEPLOY_HOST:?DEPLOY_HOST is required}"
dir="${DEPLOY_DIR:-/opt/dalan}"
keep="${KEEP_RELEASES:-5}"
wait_seconds="${READY_WAIT_SECONDS:-30}"
ready_url="${READY_URL:-http://127.0.0.1:5000/ready}"
systemctl="${SYSTEMCTL:-sudo systemctl}"

# Every server command goes through these two functions.
remote() { if [ "$host" = local ]; then bash -c "$1"; else ssh "$host" "$1"; fi; }
upload() { if [ "$host" = local ]; then cp "$1" "$2"; else scp -q "$1" "$host:$2"; fi; }

release="$dir/releases/$version"

switch_to() { remote "ln -sfn '$1' '$dir/current.tmp' && mv -T '$dir/current.tmp' '$dir/current' && $systemctl restart dalan"; }

is_ready() {
  for _ in $(seq "$wait_seconds"); do
    remote "curl -fsS -o /dev/null '$ready_url'" && return 0
    sleep 1
  done
  return 1
}

echo "Uploading $version"
remote "mkdir -p '$release'"
upload "$archive" "$release/release.tar.gz"
remote "tar -xzf '$release/release.tar.gz' -C '$release' && rm '$release/release.tar.gz' && echo 'Dalan__Version=$version' > '$release/version.env'"

previous=$(remote "readlink '$dir/current' || true")

echo "Switching to $version"
switch_to "$release"

if ! is_ready; then
  echo "Release $version is not ready after ${wait_seconds}s" >&2
  if [ -n "$previous" ]; then
    echo "Rolling back to $previous" >&2
    switch_to "$previous"
  fi
  exit 1
fi

echo "Release $version is ready. Keeping the newest $keep releases."
remote "cd '$dir/releases' && ls -1t | tail -n +$((keep + 1)) | while read -r old; do [ \"\$(readlink -f '$dir/current')\" = \"\$(readlink -f \"\$old\")\" ] || rm -rf -- \"\$old\"; done"
