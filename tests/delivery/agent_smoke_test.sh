#!/usr/bin/env bash
# Rule: AGT-01
# Publishes the agent with Native AOT for linux-x64 and runs it with no .NET on PATH.
# Any trim or AOT warning fails the build (warnings are errors in Directory.Build.props).
# Usage: tests/delivery/agent_smoke_test.sh <output-folder>
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:?output folder is required}"
fail() { echo "FAIL: $*"; exit 1; }

dotnet publish "$root/agent/Dalan.Agent/Dalan.Agent.csproj" -c Release -r linux-x64 -o "$out" -p:CopyOutputSymbolsToPublishDirectory=false

[ -f "$out/dalan" ] || fail "no agent file"
extra=$(ls "$out" | grep -vx 'dalan' || true)
[ -z "$extra" ] || fail "the agent is more than one file: $extra"

version=$(env -i PATH=/usr/bin:/bin HOME="$HOME" DOTNET_ROOT=/nonexistent "$out/dalan" version)
case "$version" in "dalan "*) ;; *) fail "dalan version said: $version" ;; esac
echo "PASS AGT-01 one native agent file runs with no .NET ($version)"
