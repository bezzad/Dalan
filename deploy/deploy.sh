#!/usr/bin/env bash
# Deploy one release to the server and roll back if it is not ready (DEL-04, DEL-05, DEL-06).
# CI pushes over SSH; the server never pulls. rsync sends only the parts that changed since the current release.
#
# Usage: deploy/deploy.sh <release folder> <version>
#
# Settings (environment):
#   DEPLOY_HOST          user@host, or "local" to run on this machine (used by tests)
#   DEPLOY_DIR           default /opt/dalan
#   KEEP_RELEASES        default 5
#   READY_WAIT_SECONDS   default 30
#   READY_URL            default http://127.0.0.1:5000/ready
#   SYSTEMCTL            default "sudo systemctl"
#   SSH_TRIES            default 5: tries to open the SSH connection (the link to Iran stalls now and then)
#   SSH_TIMEOUT          default 30: seconds for each try
#   SSH_WAIT             default 5: seconds between tries
set -euo pipefail

source="${1%/}"
version="$2"
host="${DEPLOY_HOST:?DEPLOY_HOST is required}"
dir="${DEPLOY_DIR:-/opt/dalan}"
keep="${KEEP_RELEASES:-5}"
wait_seconds="${READY_WAIT_SECONDS:-30}"
ready_url="${READY_URL:-http://127.0.0.1:5000/ready}"
systemctl="${SYSTEMCTL:-sudo systemctl}"
tries="${SSH_TRIES:-5}"
timeout="${SSH_TIMEOUT:-30}"

# DEL-07: one SSH connection for the whole deploy, opened with a few tries; every command and upload reuses it.
control=$(mktemp -d)
trap '[ "$host" = local ] || ssh -o ControlPath="$control/ssh" -O exit "$host" 2>/dev/null; rm -rf "$control"' EXIT
ssh_options="-o ControlMaster=auto -o ControlPath=$control/ssh -o ControlPersist=600 -o ConnectTimeout=$timeout -o ServerAliveInterval=10 -o ServerAliveCountMax=3"
if [ "$host" != local ]; then
  for try in $(seq "$tries"); do
    # shellcheck disable=SC2086
    ssh $ssh_options "$host" true && break
    [ "$try" -lt "$tries" ] || { echo "No SSH connection to $host after $tries tries" >&2; exit 1; }
    echo "SSH try $try failed; trying again" >&2
    sleep "${SSH_WAIT:-5}"
  done
fi

# Every server command goes through these two functions.
# shellcheck disable=SC2086
remote() { if [ "$host" = local ]; then bash -c "$1"; else ssh $ssh_options "$host" "$1"; fi; }
# upload <folder> <server folder> <basis folder>: rsync sends only the parts of each file that differ from the basis.
# A file equal to the basis (content and time) becomes a hard link. --checksum: never trust size and time alone.
upload() {
  local target="$2"; [ "$host" = local ] || target="$host:$2"
  rsync -a --checksum --compress -e "ssh $ssh_options" ${3:+--link-dest="$3"} "$1/" "$target/"
}

release="$dir/releases/$version"

switch_to() { remote "ln -sfn '$1' '$dir/current.tmp' && mv -T '$dir/current.tmp' '$dir/current' && $systemctl restart dalan"; }

is_ready() {
  for _ in $(seq "$wait_seconds"); do
    remote "curl -fsS -o /dev/null '$ready_url'" && return 0
    sleep 1
  done
  return 1
}

previous=$(remote "readlink '$dir/current' || true")

echo "Uploading $version"
remote "mkdir -p '$release'"
upload "$source" "$release" "$previous"
remote "echo 'Dalan__Version=$version' > '$release/version.env'"

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
