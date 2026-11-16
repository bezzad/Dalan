# Tasks

## 1. Session and development login (Identity)

- [ ] 1.1 Write tests for AUTH-03, AUTH-04, AUTH-05, AUTH-07; add the session token, cookie, `/api/me`, `/api/logout`; verify they pass
- [ ] 1.2 Write tests for AUTH-06; add the development login with its two switches; verify they pass

## 2. Host

- [ ] 2.1 Write tests for SEC-02 (changed), SEC-04; move the per-address limit to the host and add the security headers; verify they pass

## 3. Client API (Relay)

- [ ] 3.1 Write tests for REL-12, REL-13, REL-14, REL-15, REL-18, SEC-03, SEC-05, REL-11; build the endpoints and the per-user limit; verify they pass
- [ ] 3.2 Write tests for REL-16; build history and sync; verify they pass
- [ ] 3.3 Write tests for REL-17; add the reply keyboard to the Bot API and the message content; verify they pass

## 4. Live updates

- [ ] 4.1 Write tests for LIVE-04, LIVE-08; add the outbox table, the worker and `IMessageNotifier`; verify they pass
- [ ] 4.2 Write tests for LIVE-01, LIVE-02, LIVE-03, LIVE-05, LIVE-06, LIVE-07; build `GET /api/events`; verify they pass

## 5. PWA client

- [ ] 5.1 Write tests for CLI-04, CLI-05, CLI-06, CLI-07, CLI-08; build the pure modules; verify they pass
- [ ] 5.2 Build the screens (login, bot list, chat), styles, font, manifest and service worker; check the full flow in a real browser
- [ ] 5.3 Write tests for CLI-01, CLI-02, CLI-03, CLI-09, DEL-01; embed the client in the host and serve it; verify they pass

## 6. Finish

- [ ] 6.1 Update `docs/ARCHITECTURE.md`, `docs/DESIGN.md`, `docs/TESTING.md`, `CLAUDE.md`, `README.md`, deploy notes; verify every command in them runs as written
- [ ] 6.2 Verify CI is green, review the whole diff, archive this change
