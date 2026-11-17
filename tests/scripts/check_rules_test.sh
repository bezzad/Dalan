#!/usr/bin/env bash
# Rule: DEL-02
set -euo pipefail
script="$(cd "$(dirname "$0")/../.." && pwd)/scripts/check-rules.sh"
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

mkdir -p "$tmp/openspec/specs/demo" "$tmp/openspec/changes" "$tmp/tests"
printf '### Requirement: OPS-99 Something\n' > "$tmp/openspec/specs/demo/spec.md"

# Rule without a test: the check fails and names the rule.
if out=$(bash "$script" "$tmp"); then echo "FAIL: check passed without a test"; exit 1; fi
grep -q 'OPS-99' <<<"$out" || { echo "FAIL: output does not name OPS-99: $out"; exit 1; }

# A longer ID does not count as a test for a shorter one.
echo 'OPS-990' > "$tmp/tests/other.txt"
if bash "$script" "$tmp" >/dev/null; then echo "FAIL: OPS-990 counted as OPS-99"; exit 1; fi

# Rule with a test: the check passes.
echo '[Trait("Rule", "OPS-99")]' > "$tmp/tests/demo.cs"
bash "$script" "$tmp" >/dev/null || { echo "FAIL: check failed with a test"; exit 1; }

# A client test counts too.
rm "$tmp/tests/demo.cs"
mkdir -p "$tmp/client/tests"
echo '// Rule: OPS-99' > "$tmp/client/tests/demo.test.ts"
bash "$script" "$tmp" >/dev/null || { echo "FAIL: check failed with a client test"; exit 1; }

echo "PASS check_rules_test"
