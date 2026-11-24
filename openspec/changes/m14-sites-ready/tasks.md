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
- [x] 5.2 Levelwise again through the tunnel: first visit, service worker, SSE, offline
- [ ] 5.3 `scripts/ci.sh`
