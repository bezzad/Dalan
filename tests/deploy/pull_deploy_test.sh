#!/usr/bin/env bash
# Rules: OPS-16, OPS-17
# Rule: DEL-08
# deploy/pull-deploy.sh against a local git repository whose build and deploy scripts are fakes.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
script="$root/deploy/pull-deploy.sh"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
export RETRY_WAITS="0 0 0" DEPLOY_DIR="$tmp/dalan" DALAN_REPO="$tmp/origin.git"
mkdir -p "$DEPLOY_DIR/releases/old/agent"
echo windows > "$DEPLOY_DIR/releases/old/agent/dalan-windows-x64.exe"
echo mac > "$DEPLOY_DIR/releases/old/agent/dalan-macos-arm64"
ln -s "$DEPLOY_DIR/releases/old" "$DEPLOY_DIR/current"

# Every git call is written down, then the real git runs.
mkdir "$tmp/bin"
printf '#!/bin/bash\necho "git $*" >> "%s/git.log"\nexec %s "$@"\n' "$tmp" "$(command -v git)" > "$tmp/bin/git"
chmod +x "$tmp/bin/git"
export PATH="$tmp/bin:$PATH"

# The repository: a build that makes the Linux agent (or fails when FAIL is in the file),
# and a deploy that records its call (and fails, as after a rollback, when NOTREADY is in the file).
work="$tmp/work"; git init -q -b main "$work"
mkdir -p "$work/scripts" "$work/deploy"
cat > "$work/scripts/build-release.sh" <<'SH'
#!/bin/bash
grep -qx FAIL "$(dirname "$0")/../state" && exit 1
mkdir -p "$1/agent" && echo linux > "$1/agent/dalan-linux-x64"
SH
cat > "$work/deploy/deploy.sh" <<'SH'
#!/bin/bash
[ "$DEPLOY_HOST" = local ] || exit 9
echo "deploy $2" >> "$DEPLOY_DIR/deploys.log"; echo "$3" >> "$DEPLOY_DIR/versions.log"
if grep -qx NOTREADY "$(dirname "$0")/../state"; then echo "Release $2 is not ready after 30s" >&2; exit 1; fi
cp -r "$1" "$DEPLOY_DIR/releases/$2"
SH
commit() { echo "$1" > "$work/state"; git -C "$work" add -A; git -C "$work" -c user.name=t -c user.email=t@t commit -qm "$1"; }
deploys() { wc -l < "$DEPLOY_DIR/deploys.log"; }
commit ok
git clone -q --bare "$work" "$DALAN_REPO"
version=$(git -C "$work" rev-parse HEAD | cut -c1-12)

bash "$script" > /dev/null || fail "first run failed"
[ "$(cat "$DEPLOY_DIR/deploys.log")" = "deploy $version" ] || fail "main not deployed: $(cat "$DEPLOY_DIR/deploys.log")"
[ -f "$DEPLOY_DIR/releases/$version/agent/dalan-windows-x64.exe" ] || fail "the Windows agent of the running release was lost"
[ -f "$DEPLOY_DIR/releases/$version/agent/dalan-macos-arm64" ] || fail "the macOS agent of the running release was lost"
[ -f "$DEPLOY_DIR/releases/$version/agent/dalan-linux-x64" ] || fail "the new Linux agent is not in the release"
: > "$tmp/git.log"
bash "$script" > /dev/null || fail "second run failed"
[ "$(deploys)" = 1 ] || fail "the same commit was deployed twice"
echo "PASS OPS-16 DEL-08 the server builds and deploys a new main once, with the running Windows and macOS agents"

grep -qx "git ls-remote --exit-code $DALAN_REPO refs/heads/main" "$tmp/git.log" || fail "not asked for refs/heads/main only: $(cat "$tmp/git.log")"
[ "$(wc -l < "$tmp/git.log")" = 1 ] || fail "a run with nothing new did more than ask: $(cat "$tmp/git.log")"
echo "PASS OPS-16 nothing new: only git ls-remote of refs/heads/main"

# Another branch is never built, even when DALAN_BRANCH names it, and never fetched.
git -C "$work" checkout -q -b feature; commit feature; git -C "$work" push -q "$DALAN_REPO" feature
: > "$tmp/git.log"
DALAN_BRANCH=feature bash "$script" > /dev/null || fail "run with a new branch failed"
[ "$(deploys)" = 1 ] || fail "another branch was deployed"
grep -q feature "$tmp/git.log" && fail "another branch was asked for or fetched: $(cat "$tmp/git.log")"
git -C "$DEPLOY_DIR/src" rev-parse -q --verify refs/remotes/origin/feature >/dev/null && fail "another branch is in the server's copy"
git -C "$work" checkout -q main
echo "PASS OPS-16 another branch is never built"

commit FAIL; git -C "$work" push -q "$DALAN_REPO" main
bash "$script" > /dev/null 2>&1 && fail "a failed build must fail the run"
bash "$script" > /dev/null 2>&1 || fail "a failed commit must not be built again"
[ "$(deploys)" = 1 ] || fail "a failed build was deployed"
echo "PASS OPS-16 a failed build keeps the running release and is not tried again"

commit NOTREADY; git -C "$work" push -q "$DALAN_REPO" main
bash "$script" > /dev/null 2> "$tmp/err" && fail "a failed deploy must fail the run"
grep -q 'not ready' "$tmp/err" || fail "the reason is not in the journal: $(cat "$tmp/err")"
grep -q 'not tried again' "$tmp/err" || fail "the run does not say it gives up the commit: $(cat "$tmp/err")"
bash "$script" > /dev/null 2>&1 || fail "a failed deploy must not be deployed again"
[ "$(deploys)" = 2 ] || fail "a failed deploy was tried again: $(cat "$DEPLOY_DIR/deploys.log")"
echo "PASS OPS-16 a deploy that rolls back is in the journal and not tried again"

commit ok2; git -C "$work" push -q "$DALAN_REPO" main
: > "$tmp/git.log"
(flock 9; bash "$script" > /dev/null) 9> "$DEPLOY_DIR/pull.lock" || fail "a run that finds another one must stop quietly"
[ ! -s "$tmp/git.log" ] || fail "a second run at once did work: $(cat "$tmp/git.log")"
[ "$(deploys)" = 2 ] || fail "a second run at once deployed"
echo "PASS OPS-16 two runs never overlap"

DALAN_REPO="$tmp/nowhere.git" bash "$script" > /dev/null 2>&1 && fail "a failed fetch must fail the run"
bash "$script" > /dev/null || fail "the next run after a failed fetch failed"
[ "$(deploys)" = 3 ] || fail "the next run after a failed fetch did not deploy"
echo "PASS OPS-16 a failed fetch fails the run, the next run tries again"

# Rule: OPS-17 the version is v<VERSION>.<commits since VERSION changed>; the release folder keeps the sha name
echo 1.1 > "$work/VERSION"
commit "version 1.1"
git -C "$work" push -q "$DALAN_REPO" main
sha=$(git -C "$work" rev-parse HEAD | cut -c1-12)
bash "$script" > /dev/null || fail "the run after VERSION changed failed"
[ "$(tail -1 "$DEPLOY_DIR/deploys.log")" = "deploy $sha" ] || fail "the release is not named by the sha: $(tail -1 "$DEPLOY_DIR/deploys.log")"
[ "$(tail -1 "$DEPLOY_DIR/versions.log")" = "v1.1.0" ] || fail "a new VERSION does not start at .0: $(tail -1 "$DEPLOY_DIR/versions.log")"
[ -d "$DEPLOY_DIR/releases/$sha" ] || fail "no releases/$sha"
commit "next update"
git -C "$work" push -q "$DALAN_REPO" main
bash "$script" > /dev/null || fail "the next update failed"
[ "$(tail -1 "$DEPLOY_DIR/versions.log")" = "v1.1.1" ] || fail "the next update does not count up: $(tail -1 "$DEPLOY_DIR/versions.log")"
echo "PASS OPS-17 the version is v<VERSION>.<updates since>; each update counts up; the folder keeps the sha"

# Rule: OPS-16 the copy setup installed only fetches; the steps come from the commit it builds, so a change to this
# script reaches the server with the next build (the installed copy made the old version text, and the footer showed none).
installed="$tmp/installed-pull-deploy.sh"
sed 's/^  shown="v\$base\.\$(git.*$/  shown="old-text"/' "$script" > "$installed"
grep -q 'shown="old-text"' "$installed" || fail "the test could not make an old installed copy"
cp "$script" "$work/deploy/pull-deploy.sh"
commit "pull-deploy in the repository"
git -C "$work" push -q "$DALAN_REPO" main
before=$(deploys)
bash "$installed" > /dev/null || fail "the run of the installed copy failed"
[ "$(deploys)" = $((before + 1)) ] || fail "the commit was not deployed exactly once: $(( $(deploys) - before ))"
[ "$(tail -1 "$DEPLOY_DIR/versions.log")" = "v1.1.2" ] || fail "the old installed copy chose the version: $(tail -1 "$DEPLOY_DIR/versions.log")"
echo "PASS OPS-16 the installed copy hands over to the pull-deploy.sh of the commit it builds"

# The units and the build: every minute; /health before the switch (build-release.sh), /ready after it (deploy.sh).
grep -qx 'OnUnitInactiveSec=1min' "$root/deploy/dalan-pull.timer" || fail "the timer does not run every minute"
grep -qF 'tests/delivery/release_smoke_test.sh' "$root/scripts/build-release.sh" || fail "no /health smoke test before the switch"
grep -qF '/ready' "$root/deploy/deploy.sh" || fail "no /ready check after the switch"
echo "PASS OPS-16 every minute; /health before the switch, /ready after it"
