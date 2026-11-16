# Design

## Context

The repository has only docs. Architecture decisions are in `docs/ARCHITECTURE.md`; this file covers only how milestone 1 builds them. The cloud dev machine has only the .NET 10 SDK, so .NET 11 builds and tests run in GitHub Actions.

## Goals / Non-Goals

**Goals:**
- Projects and boundaries exactly as in `docs/ARCHITECTURE.md` section 3, guarded by tests from day one.
- A server that starts, reports health, stops cleanly, and can be deployed and rolled back.

**Non-Goals:**
- No database, outbox, business endpoint or rate limiter yet (milestone 2 brings the first public endpoint and the database).
- No `compose.yaml` yet: nothing to compose until PostgreSQL arrives.

## Decisions

1. **Versions (checked 2026-11-16):** .NET SDK 11.0.x (`global.json`, `rollForward: latestFeature`), xUnit v3 4.0.3 on Microsoft.Testing.Platform (no VSTest packages needed), `Microsoft.AspNetCore.Mvc.Testing` 11.0.0, TypeScript 7.0.2, Node 26 LTS, Caddy 2.11.9 (CI only), OpenSpec 2.0.0. Central versions in `Directory.Packages.props`.
2. **Solution format:** `Dalan.slnx` (the new XML format, default in .NET 10+).
3. **Empty services:** each service project has one public `XxxService` static class with `AddXxx(IServiceCollection)` and `MapXxx(IEndpointRouteBuilder)`, both empty now. The host calls them. This gives the architecture tests real assemblies and gives later milestones a fixed place to plug in. Alternative (no projects until needed) was rejected: the owner asked for the structure now, and the boundaries are cheapest to set before any code exists.
4. **Readiness:** `/ready` returns 503 once `IHostApplicationLifetime.ApplicationStopping` is cancelled. No extra class. Dependency checks join it in milestone 2.
5. **Version:** `/health` reads `Dalan:Version` from settings. The deploy script writes `version.env` (`Dalan__Version=<git commit>`) into the release folder and the systemd unit reads it. Default is `dev`.
6. **Logs:** the built-in JSON console formatter. No Serilog: nothing needs it yet.
7. **Architecture tests:** read `ProjectReference` items from the `.csproj` files (the compiler drops unused assembly references, so assemblies would miss some), plus reflection on type names for ARC-03. No extra library.
8. **Rule check:** `scripts/check-rules.sh` greps rule IDs from requirement headers in `openspec/specs` and active changes, then greps `tests/` for each ID.
9. **Deploy script:** plain bash. All server commands go through one function, so the test can run them locally with a fake `systemctl` and `curl`. Layout on the server: `/opt/dalan/releases/<version>/`, `/opt/dalan/current` → link, `/etc/dalan/dalan.env` for secrets (never in the repository). Keep count default 5, ready wait default 30 seconds, both script settings.
10. **Caddy:** `Caddyfile` takes domains from environment (`{$DALAN_SITE_DOMAIN}`, `{$DALAN_API_DOMAIN}`), answers 404 for `/health` and `/ready`, and forwards the rest to Kestrel on localhost.
11. **CI deploy job:** `workflow_dispatch` only, needs SSH secrets. It is written and tested but cannot run until the Iran server exists.
12. **Client:** `client/` with `package.json` (TypeScript only), `tsconfig.json` (strict) and `src/main.ts`. CI runs `tsc --noEmit`.

## Risks / Trade-offs

- [Cannot build .NET 11 on the dev machine] → CI is the source of truth; every push runs the full gate.
- [EF Core provider 11 not stable] → not used in milestone 1; milestone 2 uses version 10.x as decided.
- [Deploy untested on the real server] → shell tests cover rollback logic; the first real deploy is milestone 11.

## Migration Plan

Nothing to migrate. Rollback of this change is deleting the branch.
