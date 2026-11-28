#!/usr/bin/env bash
# Rule: CLI-90
# Rule: SEC-16
# Rule: CLI-81
# Rule: CLI-86
# The browser tests (m32 task 3.2): the built client in Chromium and in WebKit as an iPhone, with the view host on its own origin.
# Needs client/dist (the client step builds it first).
set -euo pipefail
root="$(cd "$(dirname "$0")/../.." && pwd)"
[ -f "$root/client/dist/viewer.js" ] || { echo "FAIL: build the client first (client/dist)"; exit 1; }
cd "$root/tests/client-e2e"
npm ci --no-audit --no-fund
# On GitHub the runner also gets the system libraries the browsers need.
npx playwright install ${CI:+--with-deps} chromium webkit
npx playwright test
echo "PASS CLI-81 CLI-90 SEC-16 the dashboard menu, the viewers and the message box in Chromium and in WebKit as an iPhone"
