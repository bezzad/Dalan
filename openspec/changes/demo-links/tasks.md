# Tasks

- [x] 1.1 Owner reviews this change (address on `dalans.ir`, cookie routing, builders only, limits)
- [ ] 1.2 Tests first: SITE-17, SITE-18, SITE-19, SITE-20 (Sites tests), AGT-11 (agent end-to-end test against the test server)
- [ ] 1.3 Sites: `sites.demos` table and migration, `Sites:DemoHost`, `DemoHours`, `DemoIdLength`, `MaxDemosPerBuilder` (admin settings), session with a demo, routing on the demo host, end and 410 page, certificate check
- [ ] 1.4 Agent: `dalan demo <port>`, reconnect with the same demo, end on Ctrl+C
- [ ] 1.5 Docs: DESIGN (Layer 2), ARCHITECTURE (decision), SERVER.md (`Sites__DemoHost` on the server), the guide page
- [ ] 1.6 `scripts/ci.sh` green, merge; the server builds it; set `Sites__DemoHost=dalans.ir` on the server and check one demo end to end
