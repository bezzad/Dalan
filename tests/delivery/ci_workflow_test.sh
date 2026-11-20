#!/usr/bin/env bash
# Rule: DEL-01
# Rule: DEL-08
# The CI workflow runs on every push and pull request and runs every gate.
set -euo pipefail
ci="$(cd "$(dirname "$0")/../.." && pwd)/.github/workflows/ci.yml"
fail() { echo "FAIL: $*"; exit 1; }

grep -qE '^  push:' "$ci" || fail "CI does not run on push"
grep -qE '^  pull_request:' "$ci" || fail "CI does not run on pull requests"
for gate in 'dotnet build' 'dotnet test' 'npm run check' 'npm run build' 'npm test' 'openspec@2.1.0 validate --all --strict' \
            'scripts/check-rules.sh' 'tests/scripts/check_rules_test.sh' 'tests/deploy/deploy_test.sh' \
            'tests/deploy/setup_test.sh' 'tests/deploy/backup_test.sh' 'tests/deploy/drill_test.sh' 'tests/deploy/pull_deploy_test.sh' \
            'tests/proxy/caddy_test.sh' 'tests/delivery/release_smoke_test.sh' 'tests/delivery/ci_workflow_test.sh' \
            'tests/delivery/agent_smoke_test.sh'; do
  grep -qF "$gate" "$ci" || fail "CI does not run: $gate"
done
grep -q 'continue-on-error' "$ci" && fail "a CI step may not ignore its failure"
# The test jobs run the projects in their lists: a new test project must be in one.
for project in "$(dirname "$ci")"/../../tests/Dalan.*.Tests; do
  grep -qE "^          - (.* )?$(basename "$project")( .*)?\$" "$ci" || fail "CI does not test $(basename "$project")"
done
# A draft runs no CI, but a ready pull request does.
grep -qF 'types: [opened, synchronize, reopened, ready_for_review]' "$ci" || fail "CI does not run when a pull request is ready"

echo "PASS DEL-01 CI runs every gate"

# DEL-08: every agent file the server offers (AGT-10) is built, and the deploy puts them in the release's agent folder.
for file in dalan-linux-x64 dalan-windows-x64.exe; do
  grep -qF "$file" "$ci" || fail "CI does not build $file"
  grep -qF "\"$file\"" "$(dirname "$ci")/../../src/Sites/Dalan.Sites/Downloads/AgentDownloads.cs" || fail "the server does not offer $file"
done
grep -qE '^    needs: \[.*agent.*\]' "$ci" || fail "the deploy does not wait for the agent files"
grep -qF 'mv agent-files/* out/agent/' "$ci" || fail "the deploy does not put the agent files in the release"
grep -qF '"AgentFolder": "agent"' "$(dirname "$ci")/../../src/Dalan.Host/appsettings.json" || fail "Sites:AgentFolder is not the release's agent folder"
echo "PASS DEL-08 the agent for every system is built and deployed"
