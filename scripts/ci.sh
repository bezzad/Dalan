#!/usr/bin/env bash
# DEL-01: the gate. Runs on the owner's computer every step of .github/workflows/ci.yml (which runs only by hand).
# tests/delivery/ci_workflow_test.sh fails when a step or test project is in one of the two and not in the other.
# Prints PASS or FAIL for each step and the total time, and stops at the first failure.
# A missing tool fails the run at once; only the Docker image test is skipped, and only when Docker is not there.
#
# Needs: the .NET SDK of global.json, Node of the major version in ci.yml (with npm and npx), clang and zlib1g-dev
# (Native AOT agent), caddy (proxy test), and Docker or DALAN_TEST_POSTGRES (the .NET tests). docs/TESTING.md.
# Usage: scripts/ci.sh
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 OPENSPEC_TELEMETRY=0
started=$(date +%s)
elapsed() { echo "$(( $(date +%s) - $1 ))s"; }
fail() { echo "FAIL $1 (total $(elapsed "$started"))" >&2; exit 1; }

# Tools first: a missing tool must fail the run by name, never skip a step quietly.
for tool in dotnet node npm npx clang caddy; do
  command -v "$tool" > /dev/null || fail "tools: $tool is not on the PATH (docs/TESTING.md)"
done
sdk=$(sed -nE 's/.*"version": "([0-9]+)\..*/\1/p' global.json)
[ "$(dotnet --version 2>/dev/null | cut -d. -f1)" = "$sdk" ] || fail "tools: the .NET SDK $sdk (global.json) is not on the PATH"
node_major=$(sed -nE 's/^ *node-version: ([0-9]+).*/\1/p' .github/workflows/ci.yml | head -n 1)
[ "$(node --version | sed -E 's/^v([0-9]+).*/\1/')" = "$node_major" ] || fail "tools: Node $node_major (ci.yml) is needed, not $(node --version)"
docker=no; docker info > /dev/null 2>&1 && docker=yes
[ "$docker" = yes ] || [ -n "${DALAN_TEST_POSTGRES:-}" ] || fail "tools: the .NET tests need Docker or DALAN_TEST_POSTGRES"
echo "PASS tools"

work=$(mktemp -d); trap 'rm -rf "$work"' EXIT
export work
# step <name> <commands>: one bash with -euo pipefail for each step, in the repository folder.
step() {
  local name="$1" from; from=$(date +%s)
  echo "== $name"
  bash -euo pipefail -c "$2" || fail "$name ($(elapsed "$from"))"
  echo "PASS $name ($(elapsed "$from"))"
}

# The server embeds the built client, so the client comes first.
step client 'cd client && npm ci && npm run check && npm run build && npm test'
step build 'dotnet build Dalan.slnx -c Release'
# With Docker each test project starts its own PostgreSQL; one shared DALAN_TEST_POSTGRES server takes one project at a time.
step dotnet-test 'dotnet test --solution Dalan.slnx -c Release --no-build ${DALAN_TEST_POSTGRES:+--max-parallel-test-modules 1}'
step release 'bash scripts/publish.sh "$work/out" && bash tests/delivery/release_smoke_test.sh "$work/out"'
# AGT-01: the agent is one native file. The Windows file comes only from a CI run by hand (DEL-08).
step agent 'bash tests/delivery/agent_smoke_test.sh "$work/agent" linux-x64'
step specs 'npx --yes @fission-ai/openspec@2.1.0 validate --all --strict'
step rules 'bash scripts/check-rules.sh && bash tests/scripts/check_rules_test.sh && bash tests/delivery/ci_workflow_test.sh &&
  bash tests/delivery/install_script_test.sh'
step deploy-scripts 'bash tests/deploy/deploy_test.sh && bash tests/deploy/setup_test.sh && bash tests/deploy/backup_test.sh &&
  bash tests/deploy/drill_test.sh && bash tests/deploy/pull_deploy_test.sh && bash tests/deploy/wildcard_cert_test.sh'
step proxy 'bash tests/proxy/caddy_test.sh'
step e2e 'bash tests/client-e2e/e2e_test.sh'
# The image for local runs and container platforms; production does not use it.
if [ "$docker" = yes ]; then
  step docker 'docker build -t dalan-ci . && name="dalan-ci-$$"
    docker run -d --name "$name" -p 127.0.0.1:18095:8080 dalan-ci > /dev/null
    trap "docker rm -f $name > /dev/null" EXIT
    for _ in $(seq 50); do curl -fs -o /dev/null http://127.0.0.1:18095/health && break; sleep 0.2; done
    curl -fs http://127.0.0.1:18095/health && echo'
else
  echo "SKIP docker (Docker is not there)"
fi
echo "PASS all (total $(elapsed "$started"))"
