#!/usr/bin/env bash
# Tests for deploy/backup.sh with a fake pg_dump.
set -euo pipefail
backup="$(cd "$(dirname "$0")/../.." && pwd)/deploy/backup.sh"
fail() { echo "FAIL: $*"; exit 1; }
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/bin" "$tmp/dumps"
# Fake pg_dump: writes the file after -f, or fails when the file "broken" exists.
cat > "$tmp/bin/pg_dump" <<SH
#!/bin/bash
while [ \$# -gt 0 ]; do [ "\$1" = -f ] && out="\$2"; shift; done
echo partial > "\$out"
[ ! -f "$tmp/broken" ]
SH
chmod +x "$tmp/bin/pg_dump"
export PATH="$tmp/bin:$PATH" BACKUP_DIR="$tmp/dumps" BACKUP_KEEP_DAYS=14
today="$tmp/dumps/dalan-$(date -u +%Y-%m-%d).dump"

# Rule: OPS-11 old dumps
touch -d '-20 days' "$tmp/dumps/dalan-old.dump"
touch -d '-1 day' "$tmp/dumps/dalan-yesterday.dump"
bash "$backup" >/dev/null || fail "backup failed"
[ -f "$today" ] || fail "no dump for today"
[ -f "$tmp/dumps/dalan-yesterday.dump" ] || fail "yesterday's dump deleted"
[ ! -f "$tmp/dumps/dalan-old.dump" ] || fail "20-day-old dump kept"
grep -q 'BACKUP_KEEP_DAYS=14' "$(dirname "$backup")/dalan-backup.service" || fail "unit does not start with 14 days"
grep -q 'OnCalendar=daily' "$(dirname "$backup")/dalan-backup.timer" || fail "timer is not daily"
echo "PASS OPS-11 keeps the newest days"

# Rule: OPS-11 dump fails
rm "$today"; touch "$tmp/broken"
touch -d '-20 days' "$tmp/dumps/dalan-old.dump"
if bash "$backup" >/dev/null 2>&1; then fail "backup exited with success"; fi
[ -z "$(ls "$tmp/dumps" | grep -v -e dalan-old.dump -e dalan-yesterday.dump)" ] || fail "partial dump left: $(ls "$tmp/dumps")"
[ -f "$tmp/dumps/dalan-old.dump" ] || fail "a failed backup deleted old dumps"
echo "PASS OPS-11 failed dump leaves nothing and deletes nothing"
