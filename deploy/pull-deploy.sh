#!/usr/bin/env bash
# OPS-16: the server takes main from GitHub, builds it and deploys it with rollback (deploy.sh).
# dalan-pull.timer runs it every minute as the user deploy. Each run first asks GitHub only for refs/heads/main
# (git ls-remote, small); only a new commit is fetched and built, once: a commit that is already a release,
# or that failed to build or deploy before, is skipped. A failed ls-remote or fetch is tried again on the next run.
# Messages go to the journal: journalctl -u dalan-pull.
#
# Settings (environment):
#   DEPLOY_DIR   default /opt/dalan
#   DALAN_REPO   default git@github.com:bezzad/Dalan.git (read-only deploy key, ~/.ssh/config from setup.sh)
# Only main is ever built: no setting names another branch.
set -euo pipefail
dir="${DEPLOY_DIR:-/opt/dalan}"
repo="${DALAN_REPO:-git@github.com:bezzad/Dalan.git}"
ref=refs/heads/main
src="$dir/src"

# One run at a time; a run that finds another one running just stops.
exec 9>"$dir/pull.lock"
flock -n 9 || exit 0

# GitHub may be slow or cut from Iran: three tries, then the next timer run. RETRY_WAITS is for tests.
retry() { for wait in ${RETRY_WAITS:-10 30 0}; do timeout 300 "$@" && return 0; sleep "$wait"; done; return 1; }
sha=$(retry git ls-remote --exit-code "$repo" "$ref" | cut -f1)
version="${sha::12}"
[ ! -d "$dir/releases/$version" ] || exit 0
[ "$(cat "$dir/pull-failed" 2>/dev/null)" != "$version" ] || exit 0
give_up() { echo "$version" > "$dir/pull-failed"; echo "$1 of $version failed; it is not tried again. The server keeps the running release." >&2; exit 1; }

echo "Building $version"
# Only main comes into the server's copy, never the other branches.
[ -d "$src/.git" ] || git init --quiet "$src"
retry git -C "$src" fetch --quiet --no-tags "$repo" "$ref"
git -C "$src" checkout --quiet --force --detach "$sha"
out="$dir/build"
rm -rf "$out"
# build-release.sh smoke-tests /health before the switch; deploy.sh checks /ready after it and rolls back.
bash "$src/scripts/build-release.sh" "$out" || give_up "Build"

# Agent files this server cannot build (Windows, from a manual CI run, DEL-08) stay from the running release.
for file in "$dir/current/agent/"*; do
  [ -e "$file" ] && [ ! -e "$out/agent/$(basename "$file")" ] && cp "$file" "$out/agent/"
done
# OPS-17: the shown version is v<major.minor from VERSION>.<commits on main since VERSION changed>, so every update counts up;
# the owner raises VERSION for a bigger step. The release folder keeps the sha name.
base=$(tr -d ' \n' < "$src/VERSION" 2>/dev/null || true)
since=$(git -C "$src" log -1 --format=%H -- VERSION)
if [ -n "$base" ] && [ -n "$since" ]; then
  shown="v$base.$(git -C "$src" rev-list --count --first-parent "$since..$sha")"
else
  shown="v0.0.$(git -C "$src" rev-list --count --first-parent "$sha")"
fi
DEPLOY_HOST=local DEPLOY_DIR="$dir" bash "$src/deploy/deploy.sh" "$out" "$version" "$shown" || give_up "Deploy"
rm -rf "$out"
