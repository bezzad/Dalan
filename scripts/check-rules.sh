#!/usr/bin/env bash
# DEL-02: fail when a rule ID in a spec has no test that names it.
# Usage: scripts/check-rules.sh [repo-root]
set -euo pipefail

root="${1:-$(cd "$(dirname "$0")/.." && pwd)}"

ids=$(find "$root/openspec/specs" "$root/openspec/changes" -path '*/archive' -prune -o -name spec.md -print 2>/dev/null \
  | xargs -r grep -hoE '^### Requirement: [A-Z]+-[0-9]+' \
  | awk '{print $3}' | sort -u)

missing=0
for id in $ids; do
  # Server tests are under tests/, client tests under client/tests/.
  if ! grep -rqw --exclude-dir=node_modules -- "$id" "$root/tests" "$root/client/tests" 2>/dev/null; then
    echo "No test for rule $id"
    missing=1
  fi
done

[ "$missing" -eq 0 ] && echo "All $(echo "$ids" | wc -w) rules have tests."
exit "$missing"
