#!/usr/bin/env bash
# Rule: DEL-01
# The CI workflow runs on every push and pull request and runs every gate.
set -euo pipefail
ci="$(cd "$(dirname "$0")/../.." && pwd)/.github/workflows/ci.yml"
fail() { echo "FAIL: $*"; exit 1; }

grep -qE '^  push:' "$ci" || fail "CI does not run on push"
grep -qE '^  pull_request:' "$ci" || fail "CI does not run on pull requests"
for gate in 'dotnet build' 'dotnet test' 'npm run check' 'npm run build' 'npm test' 'openspec@2.1.0 validate --all --strict' \
            'scripts/check-rules.sh' 'tests/scripts/check_rules_test.sh' 'tests/deploy/deploy_test.sh' \
            'tests/deploy/setup_test.sh' 'tests/deploy/backup_test.sh' 'tests/deploy/drill_test.sh' \
            'tests/proxy/caddy_test.sh' 'tests/delivery/release_smoke_test.sh' 'tests/delivery/ci_workflow_test.sh'; do
  grep -qF "$gate" "$ci" || fail "CI does not run: $gate"
done
grep -q 'continue-on-error' "$ci" && fail "a CI step may not ignore its failure"
# The test jobs run one project each: a new test project must be in the list.
for project in "$(dirname "$ci")"/../../tests/Dalan.*.Tests; do
  grep -qE "^          - $(basename "$project")\$" "$ci" || fail "CI does not test $(basename "$project")"
done
echo "PASS DEL-01 CI runs every gate"
