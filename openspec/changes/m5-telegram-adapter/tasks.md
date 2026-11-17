# Tasks

## 1. Contract (Relay)

- [ ] 1.1 Add `IBotGateway` and its records to `Dalan.Relay.Contracts`; implement it in Relay; make the core Bot API call it; verify every Relay test still passes

## 2. Adapter

- [ ] 2.1 Write tests for TG-01, TG-02, TG-04, TG-09, TG-10; add the route, the token check, `getMe` and the "not supported" answer; verify they pass
- [ ] 2.2 Write tests for TG-05, TG-06; add `getUpdates`; verify they pass
- [ ] 2.3 Write tests for TG-03, TG-07, TG-08; add `sendMessage` and the reply markup; verify they pass
- [ ] 2.4 Write tests for TG-11, REL-19, TG-12; verify they pass

## 3. Finish

- [ ] 3.1 Update `docs/ARCHITECTURE.md`, `docs/DESIGN.md`, `docs/TESTING.md`, `CLAUDE.md`, `README.md`
- [ ] 3.2 Verify CI is green, review the whole diff, archive this change
