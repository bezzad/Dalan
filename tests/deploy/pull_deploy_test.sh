#!/usr/bin/env bash
# Rule: OPS-16
# deploy/pull-deploy.sh against a local git repository whose build and deploy scripts are fakes.
set -euo pipefail
script="$(cd "$(dirname "$0")/../.." && pwd)/deploy/pull-deploy.sh"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
export RETRY_WAITS="0 0 0" DEPLOY_DIR="$tmp/dalan" DALAN_REPO="$tmp/origin.git"
mkdir -p "$DEPLOY_DIR/releases/old/agent"
echo windows > "$DEPLOY_DIR/releases/old/agent/dalan-windows-x64.exe"
ln -s "$DEPLOY_DIR/releases/old" "$DEPLOY_DIR/current"

# The repository: a build that makes the Linux agent (or fails when FAIL is in the file), and a deploy that records its call.
work="$tmp/work"; git init -q -b main "$work"
mkdir -p "$work/scripts" "$work/deploy"
cat > "$work/scripts/build-release.sh" <<'SH'
#!/bin/bash
grep -q FAIL "$(dirname "$0")/../state" && exit 1
mkdir -p "$1/agent" && echo linux > "$1/agent/dalan-linux-x64"
SH
cat > "$work/deploy/deploy.sh" <<'SH'
#!/bin/bash
[ "$DEPLOY_HOST" = local ] || exit 9
echo "deploy $2" >> "$DEPLOY_DIR/deploys.log"
cp -r "$1" "$DEPLOY_DIR/releases/$2"
SH
commit() { echo "$1" > "$work/state"; git -C "$work" add -A; git -C "$work" -c user.name=t -c user.email=t@t commit -qm "$1"; }
commit ok
git clone -q --bare "$work" "$DALAN_REPO"
version=$(git -C "$work" rev-parse HEAD | cut -c1-12)

bash "$script" > /dev/null || fail "first run failed"
[ "$(cat "$DEPLOY_DIR/deploys.log")" = "deploy $version" ] || fail "main not deployed: $(cat "$DEPLOY_DIR/deploys.log")"
[ -f "$DEPLOY_DIR/releases/$version/agent/dalan-windows-x64.exe" ] || fail "the Windows agent of the running release was lost"
bash "$script" > /dev/null || fail "second run failed"
[ "$(wc -l < "$DEPLOY_DIR/deploys.log")" = 1 ] || fail "the same commit was deployed twice"
echo "PASS OPS-16 the server builds and deploys a new main once"

commit FAIL; git -C "$work" push -q "$DALAN_REPO" main
bash "$script" > /dev/null 2>&1 && fail "a failed build must fail the run"
bash "$script" > /dev/null 2>&1 || fail "a failed commit must not be built again"
[ "$(wc -l < "$DEPLOY_DIR/deploys.log")" = 1 ] || fail "a failed build was deployed"
echo "PASS OPS-16 a failed build keeps the running release and is not tried again"

DALAN_REPO="$tmp/nowhere.git" bash "$script" > /dev/null 2>&1 && fail "a failed fetch must fail the run"
echo "PASS OPS-16 a failed fetch fails the run (the timer tries again)"
