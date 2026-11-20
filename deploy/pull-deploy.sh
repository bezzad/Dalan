#!/usr/bin/env bash
# OPS-16: the server takes main from GitHub, builds it and deploys it with rollback (deploy.sh).
# dalan-pull.timer runs it every few minutes as the user deploy. Each commit is built once:
# a commit that is already a release, or that failed before, is skipped. A failed fetch is tried again later.
#
# Settings (environment):
#   DEPLOY_DIR   default /opt/dalan
#   DALAN_REPO   default git@github.com:bezzad/Dalan.git (read-only deploy key, ~/.ssh/config from setup.sh)
#   DALAN_BRANCH default main
set -euo pipefail
dir="${DEPLOY_DIR:-/opt/dalan}"
repo="${DALAN_REPO:-git@github.com:bezzad/Dalan.git}"
branch="${DALAN_BRANCH:-main}"
src="$dir/src"

# One run at a time; a run that finds another one running just stops.
exec 9>"$dir/pull.lock"
flock -n 9 || exit 0

# GitHub may be slow or cut from Iran: three tries, then the next timer run. RETRY_WAITS is for tests.
retry() { for wait in ${RETRY_WAITS:-10 30 0}; do timeout 300 "$@" && return 0; sleep "$wait"; done; return 1; }
[ -d "$src/.git" ] || retry git clone --quiet --branch "$branch" "$repo" "$src"
retry git -C "$src" fetch --quiet "$repo" "$branch"
sha=$(git -C "$src" rev-parse FETCH_HEAD)
version="${sha::12}"
[ ! -d "$dir/releases/$version" ] || exit 0
[ "$(cat "$dir/pull-failed" 2>/dev/null)" != "$version" ] || exit 0

echo "Building $version"
git -C "$src" checkout --quiet --force --detach "$sha"
out="$dir/build"
rm -rf "$out"
if ! bash "$src/scripts/build-release.sh" "$out"; then
  echo "$version" > "$dir/pull-failed"
  echo "Build of $version failed; it is not tried again. The server keeps the running release." >&2
  exit 1
fi

# Agent files this server cannot build (Windows, from CI) stay from the running release.
for file in "$dir/current/agent/"*; do
  [ -e "$file" ] && [ ! -e "$out/agent/$(basename "$file")" ] && cp "$file" "$out/agent/"
done
DEPLOY_HOST=local DEPLOY_DIR="$dir" bash "$src/deploy/deploy.sh" "$out" "$version"
rm -rf "$out"
