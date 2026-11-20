#!/usr/bin/env bash
# Rule: AGT-01
# Publishes the agent with Native AOT for one system and runs it. On Linux it runs with no .NET on PATH.
# Any trim or AOT warning fails the build (warnings are errors in Directory.Build.props).
# Usage: tests/delivery/agent_smoke_test.sh <output-folder> [rid, default linux-x64] [--no-run: built for another CPU]
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:?output folder is required}"
rid="${2:-linux-x64}"
fail() { echo "FAIL: $*"; exit 1; }

dotnet publish "$root/agent/Dalan.Agent/Dalan.Agent.csproj" -c Release -r "$rid" -o "$out" -p:CopyOutputSymbolsToPublishDirectory=false

file=dalan; case "$rid" in win-*) file=dalan.exe ;; esac
[ -f "$out/$file" ] || fail "no agent file"
extra=$(ls "$out" | grep -vx "$file" || true)
[ -z "$extra" ] || fail "the agent is more than one file: $extra"

[ "${3:-}" != --no-run ] || { echo "PASS AGT-01 one native agent file for $rid (not run here)"; exit 0; }
case "$rid" in
  linux-*) version=$(env -i PATH=/usr/bin:/bin HOME="$HOME" DOTNET_ROOT=/nonexistent "$out/$file" version) ;;
  *) version=$(DOTNET_ROOT=/nonexistent "$out/$file" version) ;;
esac
case "$version" in "dalan "*) ;; *) fail "dalan version said: $version" ;; esac
echo "PASS AGT-01 one native agent file for $rid runs ($version)"
