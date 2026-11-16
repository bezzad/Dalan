# Proposal

## Why

Dalan has a design but no code. Milestone 1 (docs/DESIGN.md section 19) gives every later milestone a safe base: a solution with the agreed service boundaries, a server that starts and reports its health, CI that blocks bad changes, and a no-Docker deploy with rollback.

## What Changes

- New .NET 11 solution: one host process and empty projects for the four services (Identity, Relay, Notification, Admin), each with a `.Contracts` project, plus the Telegram adapter plugin.
- `GET /health` (alive) and `GET /ready` (ready for traffic) endpoints, graceful shutdown, JSON logs on stdout, settings from config files and environment.
- Architecture tests that fail the build when a service boundary is broken.
- CI on GitHub Actions: build, all tests, `openspec validate --strict`, and a check that every rule ID has a test.
- Deploy script: self-contained linux-x64 binary, copy over SSH, switch, check `/ready`, roll back on failure. Manual trigger only until the Iran server exists.
- `Dockerfile` for local runs and container platforms. Production stays on systemd.
- Empty PWA folder that type-checks in CI.
- `CLAUDE.md`, `README.md`, `docs/ARCHITECTURE.md`, `docs/TESTING.md`.

Out of scope: database, any business endpoint, rate limiting (first public endpoint comes in milestone 2), real client screens.

## Capabilities

### New Capabilities

- `operations`: health, readiness, shutdown, logging and configuration behavior of the running server.
- `architecture`: dependency rules between services, contracts and plugins.
- `delivery`: CI gates, release artifact and deploy with rollback.

### Modified Capabilities

None.

## Impact

- New code under `src/`, `tests/`, `client/`, `deploy/`, `scripts/`, `.github/workflows/`.
- New dev dependency: OpenSpec CLI 2.0.0 (dev and CI only).
- No runtime dependency outside the .NET 11 shared framework in this milestone.
