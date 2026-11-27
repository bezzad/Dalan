#!/usr/bin/env bash
# OPS-17: the shown version of a commit: v<VERSION file>.<first-parent commits since VERSION last changed>.
# Usage: deploy/version.sh <git checkout> <commit>
set -euo pipefail
src="$1"
sha="$2"
base=$(git -C "$src" show "$sha:VERSION" 2>/dev/null | tr -d ' \n' || true)
since=$(git -C "$src" log -1 --format=%H "$sha" -- VERSION)
if [ -n "$base" ] && [ -n "$since" ]; then echo "v$base.$(git -C "$src" rev-list --count --first-parent "$since..$sha")"
else echo "v0.0.$(git -C "$src" rev-list --count --first-parent "$sha")"; fi
