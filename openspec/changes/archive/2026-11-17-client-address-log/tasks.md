# Tasks

## 1. Real client address (host)

- [x] 1.1 Write tests for SEC-10, SEC-02 (new scenario); read `Dalan:TrustedProxies` into the forwarded-headers options; verify they pass
- [x] 1.2 Write tests for SEC-11; add the warning; verify they pass
- [x] 1.3 Write the proxy test for SEC-10; make Caddy send `address:port`; verify it passes

## 2. Access log (Admin, Relay)

- [x] 2.1 Add `IAccessLog` to Admin.Contracts, the `admin` schema, table and migration
- [x] 2.2 Write tests for ADM-01; record in the Bot API and Client API filters; verify they pass
- [x] 2.3 Write tests for ADM-02, ADM-03; add the interval and the sweep; verify they pass

- [x] 2.4 Write tests for ADM-04; record each sent SMS code in Identity; verify they pass

## 3. Finish

- [x] 3.1 Update `docs/ARCHITECTURE.md`, `docs/DESIGN.md`, `docs/TESTING.md`, `CLAUDE.md`, deploy notes
- [x] 3.2 Verify CI is green, review the whole diff, archive this change
