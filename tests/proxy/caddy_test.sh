#!/usr/bin/env bash
# Rule: OPS-06
# Rule: SEC-10
# Rule: OPS-15
# Runs deploy/Caddyfile with a stub upstream. Needs the caddy binary on PATH.
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
fail() { echo "FAIL: $*"; exit 1; }
command -v caddy >/dev/null || fail "caddy is not installed"

tmp=$(mktemp -d)
cleanup() { kill "${upstream:-}" "${proxy:-}" 2>/dev/null || true; rm -rf "$tmp"; }
trap cleanup EXIT

# The stub upstream answers every path with the X-Forwarded-For header it got.
cat > "$tmp/upstream.py" <<'PY'
from http.server import BaseHTTPRequestHandler, HTTPServer

import sys

class Echo(BaseHTTPRequestHandler):
    def do_GET(self):
        # SITE-17 stub: write down each certificate question and say no.
        if self.path.startswith("/api/sites/tls"):
            with open(sys.argv[1], "a") as asked:
                asked.write(self.path + "\n")
            self.send_response(404)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        body = ",".join(self.headers.get_all("X-Forwarded-For") or []).encode()
        self.send_response(200)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *args):
        pass

HTTPServer(("127.0.0.1", 18081), Echo).serve_forever()
PY
python3 "$tmp/upstream.py" "$tmp/asked.log" & upstream=$!

DALAN_SITE_DOMAIN=http://localhost:18080 DALAN_API_DOMAIN=http://127.0.0.1:18080 DALAN_TUNNEL_DOMAIN=http://tunnel.localhost:18080 \
  DALAN_UPSTREAM=127.0.0.1:18081 DALAN_HTTP_PORT=18082 DALAN_HTTPS_PORT=18443 \
  caddy run --config "$root/deploy/Caddyfile" --adapter caddyfile >"$tmp/caddy.log" 2>&1 & proxy=$!

# Wait for a 200: Caddy may answer 502 before the stub upstream listens.
for _ in $(seq 50); do curl -fs -o /dev/null http://127.0.0.1:18080/ && break; sleep 0.2; done

[ "$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:18080/)" = 200 ] || fail "proxy does not forward normal paths"
for probe in /health /ready /api/sites/tls; do
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:18080$probe")
  [ "$code" = 404 ] || fail "$probe through the proxy gave $code, want 404"
done
echo "PASS OPS-06 probes are private"

# The client fakes the header; the upstream must get only the client's real address and port.
got=$(curl -s -H 'X-Forwarded-For: 9.9.9.9' -w ' %{local_port}' http://127.0.0.1:18080/)
[ "${got% *}" = "127.0.0.1:${got#* }" ] || fail "upstream got X-Forwarded-For '${got% *}', want '127.0.0.1:${got#* }'"
echo "PASS SEC-10 front proxy sends the real address and port"

[ "$(curl -s -o /dev/null -w '%{http_code}' --resolve tunnel.localhost:18080:127.0.0.1 http://tunnel.localhost:18080/)" = 200 ] \
  || fail "proxy does not forward the tunnel host"

# OPS-15: any other HTTPS host asks Dalan before Caddy gets a certificate; Dalan said no, so the handshake fails.
curl -s -k --noproxy "*" -o /dev/null --max-time 10 --resolve shop.sites.test:18443:127.0.0.1 https://shop.sites.test:18443/ \
  && fail "a host Dalan did not allow got a certificate"
grep -qx '/api/sites/tls?domain=shop.sites.test' "$tmp/asked.log" || fail "Caddy did not ask Dalan about shop.sites.test"
echo "PASS OPS-15 tunnel host is served and a site host gets a certificate only when Dalan allows it"
