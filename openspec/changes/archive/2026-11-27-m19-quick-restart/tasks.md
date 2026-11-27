# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11).

- [x] 1. Test in `tests/Dalan.Host.Tests/ShutdownTests.cs`: live stream, bot long poll and site tunnel open; stop; host gone within `Dalan:StopGraceSeconds`; tunnel closed with 1001 (OPS-03)
- [x] 2. Find what holds the stop (first suspect: `TunnelHub`, `TunnelSession`, `TunnelEndpoints`); end it on `ApplicationStopping` (OPS-03)
- [x] 3. Background workers (`OutboxWorker`, `PushWorker`, sweepers) stop on their token at once (OPS-03)
- [x] 4. `Dalan:StopGraceSeconds` 5 in `appsettings.json`; DESIGN section 18; `docs/SERVER.md` deploy gap
- [x] 5. CI green on GitHub, final review, merge (#92)
- [x] 6. After the next deploy: `journalctl -u dalan` shows `Stopping` to `Stopped` within 5 s, and Caddy has no `connection refused` burst; archive

Archived 2026-11-27 with its code merged; the open checks moved to `openspec/OWNER-CHECKS.md`.
