# Proposal

## Why

GitHub Actions stopped on 2026-11-20: the account's minutes ran out, so nothing was tested or deployed. The owner wants the server to take main from GitHub and build it itself whenever it can reach GitHub, with the push from CI and the deploy from the owner's computer as other ways.

## What Changes

- **Build on the server** (OPS-16): a timer runs `deploy/pull-deploy.sh` as the user `deploy`: fetch main (three tries; GitHub over SSH on port 443), build it once with `scripts/build-release.sh` (server, client checks, smoke test, Linux agent), then the same `deploy.sh` with rollback. A failed build is not tried again; the running release stays. `install.sh` with `PULL_DEPLOY=1` brings the .NET SDK and Node from the owner's computer; setup makes a read-only GitHub key and prints it. Packages come from the caches after the first build.
- **DEL-06** now allows this pull; the push from CI stays.
- **Fewer CI minutes** (DEL-01): no CI on a draft pull request; the .NET tests run on two machines instead of seven.

Out of scope: the full .NET tests on the server (they need a test database server; they stay in CI); a mirror of the repository inside Iran.
