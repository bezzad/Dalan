#!/usr/bin/env bash
# Build and check one full release: the server (DEL-03), the client tests, and the Linux agent in agent/ (AGT-10).
# Used by deploy/pull-deploy.sh on the server (OPS-16) and by hand on the owner's computer.
# Usage: scripts/build-release.sh <output-folder>
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${1:?output folder is required}"
work=$(mktemp -d); trap 'rm -rf "$work"' EXIT

bash "$root/scripts/publish.sh" "$out"
(cd "$root/client" && npm run check && npm test)
bash "$root/tests/delivery/release_smoke_test.sh" "$out"
bash "$root/tests/delivery/agent_smoke_test.sh" "$work/agent" linux-x64
mkdir -p "$out/agent"
mv "$work/agent/dalan" "$out/agent/dalan-linux-x64"
