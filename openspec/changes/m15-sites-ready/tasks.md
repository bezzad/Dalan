# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code, then run them.

## 1. Proxy fixes

- [x] 1.1 No body for 1xx, 204, 304 and `HEAD` (SITE-08)
- [x] 1.2 `Sites:Domain` and `Sites:DemoHost` with a port (SITE-08)
- [x] 1.3 The line per address: `MaxPendingPerAddress` 200, `MaxQueuedPerAddress` 1000 (SITE-12)

## 2. Streaming

- [x] 2.1 Server: `more`, parts, ack, cancel, 410, `MaxAnswerMb`; first-message timeout (SITE-22, SITE-10, SITE-11)
- [x] 2.2 Agent: parts with flush, window, cancel (AGT-07, AGT-12)

## 3. Session end

- [x] 3.1 Server: `DELETE /api/tunnel/sessions/{id}`; end on WebSocket close (SITE-23)
- [x] 3.2 Agent: end the session on Ctrl+C (AGT-13)

## 4. Names and service

- [x] 4.1 Server: `POST /api/sites` (SITE-24)
- [x] 4.2 Agent: `dalan service install|uninstall`, `dalan status` (AGT-14, AGT-15)
- [x] 4.3 Client: Reserve, list refresh, guide steps "Try it" and "Keep it online" in My apps and `/guide/site` (CLI-38, CLI-39)

## 5. Docs and check

- [x] 5.1 `docs/DESIGN.md` (section 11 limits, section 18 values), `docs/ARCHITECTURE.md` decision, `llms.txt`
- [x] 5.2 Mysite again through the tunnel: first visit, service worker, SSE, offline
- [ ] 5.3 `scripts/ci.sh`

## 6. Remaining (stopped 2026-11-24: weekly usage limit and PC load)

State: all code, tests and docs are in commit `ca929f7` on branch `m15-sites-ready` (pushed, not merged). Passed: Sites tests (server and agent), client check/build/151 tests, `check-rules.sh`, `openspec validate --all --strict`, Mysite by hand through the tunnel. The full gate stopped inside the .NET tests (no failure seen).

- [ ] 6.1 Run the gate at low priority: `nice -n 19 bash scripts/ci.sh` (this PC: Node 26 and the workarounds in memory "local-gate-quirks")
- [ ] 6.2 Final review of the whole diff (rule 11); known limits to report: a streamed answer has no `Content-Length`; Windows does not restart the agent after a crash
- [ ] 6.3 Untested on real systems: `dalan service install` on Linux (systemd user unit), macOS (launchd), Windows (Run key, `reg add` quoting)
- [ ] 6.4 Merge to main, then `openspec archive m15-sites-ready`; check the live server builds it (`readlink /opt/dalan/current`)
- [ ] 6.5 Owner: put `dalans.ir` on the Public Suffix List; decide on a wildcard certificate (Let's Encrypt gives about 50 new names a week)
- [ ] 6.6 Mysite side: build with `PUBLIC_POCKETBASE_URL=https://mysite.dalans.ir`; trust `X-Forwarded-For` in PocketBase; add the site address to OAuth redirects, Turnstile hostnames and `--origins`; fetch about 6 files at a time in `src/sw.js`
- [ ] 6.7 Next milestones (approved idea, not specced): m15 static upload and custom domain, m16 signed user header, m17 inspector, Basic Auth and IP allowlist
