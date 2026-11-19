#!/usr/bin/env bash
# Tests for deploy/deploy.sh with a fake server folder, a fake systemctl and a fake curl.
set -euo pipefail
deploy="$(cd "$(dirname "$0")/../.." && pwd)/deploy/deploy.sh"
fail() { echo "FAIL: $*"; exit 1; }
base=$(mktemp -d); trap 'rm -rf "$base"' EXIT

setup() {
  tmp=$(mktemp -d -p "$base")
  mkdir -p "$tmp/bin" "$tmp/server/releases" "$tmp/pkg"
  echo '#!/bin/sh' > "$tmp/pkg/Dalan.Host"
  chmod +x "$tmp/pkg/Dalan.Host"
  echo '{}' > "$tmp/pkg/appsettings.json"
  # Fake curl: ready only when the file "ready" exists. Logs every URL.
  cat > "$tmp/bin/curl" <<SH
#!/bin/sh
for a in "\$@"; do case "\$a" in http*) echo "\$a" >> "$tmp/curl.log";; esac; done
[ -f "$tmp/ready" ]
SH
  printf '#!/bin/sh\necho "$@" >> %s/systemctl.log\n' "$tmp" > "$tmp/bin/systemctl"
  for tool in git wget ssh scp; do printf '#!/bin/sh\necho %s >> %s/forbidden.log\nexit 1\n' "$tool" "$tmp" > "$tmp/bin/$tool"; done
  chmod +x "$tmp/bin/"*
  export PATH="$tmp/bin:$PATH" DEPLOY_HOST=local DEPLOY_DIR="$tmp/server" SYSTEMCTL="$tmp/bin/systemctl" \
         READY_WAIT_SECONDS=2 READY_URL=http://127.0.0.1:5000/ready KEEP_RELEASES=5
}

current() { readlink "$tmp/server/current"; }

# Rule: DEL-04 healthy release
setup
touch "$tmp/ready"
bash "$deploy" "$tmp/pkg" v1 >/dev/null || fail "healthy deploy exited with error"
[ "$(current)" = "$tmp/server/releases/v1" ] || fail "current is $(current), want v1"
grep -q 'Dalan__Version=v1' "$tmp/server/releases/v1/version.env" || fail "version.env not written"
grep -q 'restart dalan' "$tmp/systemctl.log" || fail "service not restarted"
[ -x "$tmp/server/releases/v1/Dalan.Host" ] || fail "Dalan.Host is not executable"
echo "PASS DEL-04 healthy release"

# Rule: DEL-04 only changed files are sent: an unchanged file is a hard link to the current release
echo '{"changed":true}' > "$tmp/pkg/appsettings.json"
bash "$deploy" "$tmp/pkg" v1b >/dev/null || fail "second deploy failed"
[ "$tmp/server/releases/v1/Dalan.Host" -ef "$tmp/server/releases/v1b/Dalan.Host" ] || fail "unchanged file was copied, not linked"
grep -q changed "$tmp/server/releases/v1b/appsettings.json" || fail "changed file was not sent"
grep -q '{}' "$tmp/server/releases/v1/appsettings.json" || fail "the old release was changed"
echo "PASS DEL-04 only changed files are sent"

# Rule: DEL-04 broken release
rm "$tmp/ready"
if bash "$deploy" "$tmp/pkg" v2 >/dev/null 2>&1; then fail "broken deploy exited with success"; fi
[ "$(current)" = "$tmp/server/releases/v1b" ] || fail "current is $(current), want rollback to v1b"
[ "$(grep -c 'restart dalan' "$tmp/systemctl.log")" -eq 4 ] || fail "want 4 restarts (v1, v1b, v2, rollback)"
echo "PASS DEL-04 broken release rolls back"

# Rule: DEL-05 keep the newest releases
setup
touch "$tmp/ready"
for i in 1 2 3 4 5 6; do mkdir -p "$tmp/server/releases/old$i"; touch -d "-$((10 - i)) hours" "$tmp/server/releases/old$i"; done
bash "$deploy" "$tmp/pkg" v7 >/dev/null || fail "deploy failed"
left=$(ls -1 "$tmp/server/releases" | sort | tr '\n' ' ')
[ "$left" = "old3 old4 old5 old6 v7 " ] || fail "kept releases: $left"
echo "PASS DEL-05 keeps the newest 5"

# Rule: DEL-05 never delete the current release, even when other folders look newer
# (for example after the server clock moved back).
setup
touch "$tmp/ready"
mkdir -p "$tmp/server/releases/future"
touch -d '+1 day' "$tmp/server/releases/future"
KEEP_RELEASES=1 bash "$deploy" "$tmp/pkg" v1 >/dev/null || fail "deploy failed"
[ -d "$tmp/server/releases/v1" ] || fail "current release was deleted"
echo "PASS DEL-05 current release is kept"

# Rule: DEL-06 the deploy calls nothing but the local ready URL; no git, wget, ssh or scp
[ ! -f "$tmp/forbidden.log" ] || fail "forbidden tools called: $(cat "$tmp/forbidden.log")"
[ "$(sort -u "$tmp/curl.log")" = "http://127.0.0.1:5000/ready" ] || fail "curl called: $(sort -u "$tmp/curl.log")"
echo "PASS DEL-06 no pull from outside"
