#!/usr/bin/env bash
# Rule: DEL-01
# Rule: DEL-06
# Rule: DEL-08
# The gate is scripts/ci.sh on the owner's computer; the GitHub workflow runs the same steps, only by hand.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
ci="$root/.github/workflows/ci.yml"
local_ci="$root/scripts/ci.sh"
fail() { echo "FAIL: $*"; exit 1; }

[ -f "$local_ci" ] || fail "scripts/ci.sh is missing"
# The steps both must run, word for word.
for gate in 'npm ci' 'npm run check' 'npm run build' 'npm test' 'dotnet build Dalan.slnx -c Release' \
            'openspec@2.1.0 validate --all --strict' 'docker build'; do
  grep -qF "$gate" "$ci" || fail "the workflow does not run: $gate"
  grep -qF "$gate" "$local_ci" || fail "scripts/ci.sh does not run: $gate"
done
# Every script either one runs, the other runs too; every test script in tests/ is in both.
scripts() { grep -oE '(tests|scripts)/[A-Za-z0-9_./-]+\.sh' "$1" | sort -u; }
[ "$(scripts "$ci")" = "$(scripts "$local_ci")" ] ||
  fail "the workflow and scripts/ci.sh run different scripts: $(diff <(scripts "$ci") <(scripts "$local_ci") | grep '^[<>]' | tr '\n' ' ')"
for test in "$root"/tests/*/*_test.sh; do
  file="${test#"$root/"}"
  scripts "$local_ci" | grep -qx "$file" || fail "scripts/ci.sh does not run $file"
done
# The test jobs run the projects in their lists, ci.sh runs the solution: a new test project must be in both.
for project in "$root"/tests/Dalan.*.Tests; do
  name=$(basename "$project")
  grep -qE "^          - (.* )?$name( .*)?\$" "$ci" || fail "the workflow does not test $name"
  grep -qF "tests/$name/$name.csproj" "$root/Dalan.slnx" || fail "Dalan.slnx (scripts/ci.sh) does not test $name"
done
grep -qF 'dotnet test' "$local_ci" || fail "scripts/ci.sh does not run the .NET tests"
grep -q 'continue-on-error' "$ci" && fail "a CI step may not ignore its failure"
grep -qx 'set -euo pipefail' "$local_ci" || fail "scripts/ci.sh does not stop on errors"
echo "PASS DEL-01 the workflow and scripts/ci.sh run the same gates"

# ci.sh with fake tools: a missing tool fails at once by name; a failing step prints FAIL and stops the run.
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
mkdir "$tmp/bin"
for tool in bash env dirname date cat grep sed head cut mktemp rm; do ln -s "$(command -v "$tool")" "$tmp/bin/$tool"; done
printf '#!/bin/bash\necho 11.0.100\n' > "$tmp/bin/dotnet"
printf '#!/bin/bash\necho v%s.1.0\n' "$(sed -nE 's/^ *node-version: ([0-9]+).*/\1/p' "$ci" | head -n 1)" > "$tmp/bin/node"
printf '#!/bin/bash\necho "npm $*" >> "%s/npm.log"; exit 1\n' "$tmp" > "$tmp/bin/npm"
for tool in npx clang caddy docker; do printf '#!/bin/bash\nexit 0\n' > "$tmp/bin/$tool"; done
(cd "$tmp/bin" && chmod +x dotnet node npm npx clang caddy docker)
mv "$tmp/bin/caddy" "$tmp/caddy"
out=$(PATH="$tmp/bin" bash "$local_ci" 2>&1) && fail "ci.sh passed without caddy"
grep -q 'caddy' <<< "$out" || fail "ci.sh does not name the missing tool: $out"
[ ! -f "$tmp/npm.log" ] || fail "ci.sh ran a step before it found a missing tool"
mv "$tmp/caddy" "$tmp/bin/caddy"
out=$(PATH="$tmp/bin" bash "$local_ci" 2>&1) && fail "ci.sh passed with a failing step"
grep -q '^FAIL client' <<< "$out" || fail "ci.sh does not say which step failed: $out"
[ "$(cat "$tmp/npm.log")" = "npm ci" ] || fail "ci.sh went on after a failing step: $(cat "$tmp/npm.log")"
echo "PASS DEL-01 scripts/ci.sh fails on a missing tool and stops at the first failing step"

# No push, pull request or merge starts GitHub CI or a deploy; a run by hand deploys only with deploy on.
grep -qE '^  (push|pull_request|pull_request_target|schedule):' "$ci" && fail "the workflow starts by itself"
grep -qE '^  workflow_dispatch:' "$ci" || fail "the workflow cannot run by hand"
grep -qxF '    if: inputs.deploy' "$ci" || fail "the deploy does not wait for deploy to be ticked"
echo "PASS DEL-06 the server builds main; GitHub deploys only when run by hand"

# DEL-08: every agent file the server offers (AGT-10) is built by a run by hand, and the deploy puts them in the release's agent folder.
for file in dalan-linux-x64 dalan-windows-x64.exe; do
  grep -qF "$file" "$ci" || fail "CI does not build $file"
  grep -qF "\"$file\"" "$root/src/Sites/Dalan.Sites/Downloads/AgentDownloads.cs" || fail "the server does not offer $file"
done
grep -qE '^    needs: \[.*agent.*\]' "$ci" || fail "the deploy does not wait for the agent files"
grep -qF 'mv agent-files/* out/agent/' "$ci" || fail "the deploy does not put the agent files in the release"
grep -qF '"AgentFolder": "agent"' "$root/src/Dalan.Host/appsettings.json" || fail "Sites:AgentFolder is not the release's agent folder"
echo "PASS DEL-08 the agent for every system is built and deployed"
