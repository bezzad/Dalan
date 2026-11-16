# Tasks

## 1. Solution and projects

- [x] 1.1 Add `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `Dalan.slnx`; verify `dotnet build` succeeds in CI
- [x] 1.2 Add service projects and `.Contracts` (Identity, Relay, Notification, Admin), Telegram plugin and `Dalan.Host`, each with its empty `AddXxx`/`MapXxx`; verify the host builds
- [x] 1.3 Write architecture tests for ARC-01 to ARC-05 (each with a failing case proven by a test helper on a fake reference graph); verify they pass on the real solution

## 2. Host behavior

- [x] 2.1 Write tests for OPS-01, OPS-02, OPS-05 with `WebApplicationFactory`; see them fail; implement `/health`, `/ready` and settings; verify they pass
- [x] 2.2 Write test for OPS-03 (request in flight during stop) and OPS-04 (JSON log line); implement shutdown timeout and JSON console logs; verify they pass

## 3. Delivery

- [x] 3.1 Write `scripts/check-rules.sh` and its shell test for DEL-02; verify it passes on the repo and fails on a spec with an untested ID
- [x] 3.2 Write `deploy/deploy.sh`, `deploy/dalan.service` and shell tests for DEL-04, DEL-05, DEL-06 with fake `systemctl` and `curl`; verify the tests pass
- [x] 3.3 Write `deploy/Caddyfile` and a shell test for OPS-06 that runs Caddy in CI with a stub upstream; verify `/health` gives 404 through Caddy
- [ ] 3.4 Add `.github/workflows/ci.yml` (build, test, client type-check, `openspec validate --all --strict`, rule check, publish self-contained binary and smoke test for DEL-03) and a manual deploy job; verify CI is green on the pull request
- [ ] 3.5 Add `Dockerfile` (multi-stage, runs the host); verify `docker build` and `/health` in CI

## 4. Client and docs

- [x] 4.1 Add `client/` (package.json, strict tsconfig, empty `src/main.ts`, `index.html` with `dir="rtl" lang="fa"`); verify `tsc --noEmit` passes in CI
- [x] 4.2 Write `CLAUDE.md` (summary, commands, layout, rules) and update `README.md`; verify every command in them runs as written

## Workflow follow-up

- Owner reviews the pull request.
- Archive the change with `openspec archive m1-skeleton` after merge, so `openspec/specs` holds operations, architecture and delivery.
