#!/usr/bin/env bash
# Rule: OPS-06
# Runs deploy/Caddyfile with a stub upstream. Needs the caddy binary on PATH.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
fail() { echo "FAIL: $*"; exit 1; }
command -v caddy >/dev/null || fail "caddy is not installed"

tmp=$(mktemp -d)
cleanup() { kill "${upstream:-}" "${proxy:-}" 2>/dev/null || true; rm -rf "$tmp"; }
trap cleanup EXIT

echo "from upstream" > "$tmp/index.html"
cp "$tmp/index.html" "$tmp/health"
python3 -m http.server 18081 --bind 127.0.0.1 --directory "$tmp" >/dev/null 2>&1 & upstream=$!

DALAN_SITE_DOMAIN=http://localhost:18080 DALAN_API_DOMAIN=http://127.0.0.1:18080 DALAN_UPSTREAM=127.0.0.1:18081 \
  caddy run --config "$root/deploy/Caddyfile" --adapter caddyfile >"$tmp/caddy.log" 2>&1 & proxy=$!

for _ in $(seq 50); do curl -s -o /dev/null http://localhost:18080/ && break; sleep 0.2; done

[ "$(curl -s -o /dev/null -w '%{http_code}' http://localhost:18080/)" = 200 ] || fail "proxy does not forward normal paths"
for probe in /health /ready; do
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:18080$probe")
  [ "$code" = 404 ] || fail "$probe through the proxy gave $code, want 404"
done
echo "PASS OPS-06 probes are private"
