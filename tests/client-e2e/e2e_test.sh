#!/usr/bin/env bash
# Rule: CLI-90
# Rule: SEC-16
# Rule: CLI-81
# Rule: CLI-86
# Rule: CLI-100
# Rule: CLI-101
# Rule: CLI-102
# Rule: CLI-26
# Rule: CLI-103
# Rule: CLI-104
# Rule: CLI-105
# Rule: CLI-106
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
echo "PASS CLI-26 CLI-81 CLI-90 CLI-101 CLI-102 CLI-103 CLI-104 CLI-105 CLI-106 SEC-16 the home page, the dashboard menu, the guide layout, the viewers and the message box in Chromium and in WebKit as an iPhone"
