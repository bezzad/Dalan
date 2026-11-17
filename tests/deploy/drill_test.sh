#!/usr/bin/env bash
# Tests for deploy/drill.sh with fake nft, systemd-run, systemctl and journalctl.
set -euo pipefail
drill="$(cd "$(dirname "$0")/../.." && pwd)/deploy/drill.sh"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/bin"
# Fake nft: "-f -" saves the rule set; list succeeds while it exists; delete removes it.
cat > "$tmp/bin/nft" <<SH
#!/bin/bash
case "\$1" in
  -f) cat > "$tmp/ruleset";;
  list) [ -f "$tmp/ruleset" ];;
  delete) echo "delete \$*" >> "$tmp/nft.log"; rm -f "$tmp/ruleset";;
esac
SH
printf '#!/bin/sh\necho "$@" >> %s/systemd-run.log\n' "$tmp" > "$tmp/bin/systemd-run"
printf '#!/bin/sh\nexit 0\n' > "$tmp/bin/systemctl"
cat > "$tmp/bin/journalctl" <<'SH'
#!/bin/sh
echo 'dalan-drill IN= OUT=eth0 SRC=10.0.0.5 DST=8.8.8.8 LEN=60 PROTO=UDP SPT=41000 DPT=53 LEN=40'
echo 'dalan-drill IN= OUT=eth0 SRC=10.0.0.5 DST=142.250.1.1 LEN=60 PROTO=TCP SPT=41001 DPT=443 WINDOW=64240 SYN'
echo 'dalan-drill IN= OUT=eth0 SRC=10.0.0.5 DST=142.250.1.1 LEN=60 PROTO=TCP SPT=41002 DPT=443 WINDOW=64240 SYN'
echo 'dalan-drill IN= OUT=eth0 SRC=10.0.0.5 DST=8.8.8.8 LEN=60 PROTO=UDP SPT=41003 DPT=53 LEN=40'
SH
chmod +x "$tmp/bin"/*
printf '# Iranian ranges\n5.160.0.0/16\n\n2a01:5ec0::/29\r\n' > "$tmp/allow.txt"
echo 'DRILL_MINUTES=45' > "$tmp/drill.env"
export PATH="$tmp/bin:$PATH" DRILL_ENV="$tmp/drill.env" DRILL_ALLOW_FILE="$tmp/allow.txt" DRILL_STATE="$tmp/start"

# Rule: OPS-12 drill on
bash "$drill" start >/dev/null || fail "start failed"
rules="$tmp/ruleset"
grep -q 'hook output' "$rules" || fail "not an output hook"
grep -q 'hook input' "$rules" && fail "inbound is blocked"
grep -q 'oif "lo" accept' "$rules" || fail "loopback not allowed"
grep -q 'ct state established,related accept' "$rules" || fail "replies not allowed"
grep -q 'allow4 .*192.168.0.0/16, 100.64.0.0/10, 169.254.0.0/16, 5.160.0.0/16 }' "$rules" || fail "IPv4 allow list: $(grep allow4 "$rules" | head -1)"
grep -q 'allow6 .*fe80::/10, 2a01:5ec0::/29 }' "$rules" || fail "IPv6 allow list: $(grep allow6 "$rules" | head -1)"
grep -q 'log prefix "dalan-drill "' "$rules" || fail "blocked calls are not logged"
grep -qx '    drop' "$rules" || fail "the rest is not dropped"
grep -q -- "--on-active=45m $drill stop" "$tmp/systemd-run.log" || fail "no stop after DRILL_MINUTES: $(cat "$tmp/systemd-run.log")"
[ -f "$tmp/start" ] || fail "start time not saved"
# The real nft checks the syntax, where it can (CI has passwordless sudo).
if [ -x /usr/sbin/nft ] && sudo -n true 2>/dev/null; then sudo -n /usr/sbin/nft -c -f "$rules" || fail "nft refuses the rule set"; fi
echo "PASS OPS-12 drill on blocks outbound and stops by itself"

# Rule: OPS-12 drill on without an allow list
rm "$tmp/allow.txt"
DRILL_ENV=/nonexistent bash "$drill" start >/dev/null || fail "start without allow list failed"
grep -q 'allow4 .*169.254.0.0/16 }' "$rules" || fail "IPv4 set without allow list: $(grep allow4 "$rules" | head -1)"
grep -q -- "--on-active=60m" "$tmp/systemd-run.log" || fail "default is not 60 minutes"
echo "PASS OPS-12 drill on with private ranges only"

# Rule: OPS-12 drill off
out=$(bash "$drill" stop) || fail "stop failed"
[ ! -f "$rules" ] || fail "block not removed"
[ "$(echo "$out" | grep -c '8.8.8.8 (ports 53)')" = 1 ] || fail "8.8.8.8 not listed once: $out"
[ "$(echo "$out" | grep -c '142.250.1.1 (ports 443)')" = 1 ] || fail "142.250.1.1 not listed once: $out"
[ ! -f "$tmp/start" ] || fail "start time not cleared"
bash "$drill" stop >/dev/null || fail "stop with no drill failed"
echo "PASS OPS-12 drill off lists each blocked address once"
