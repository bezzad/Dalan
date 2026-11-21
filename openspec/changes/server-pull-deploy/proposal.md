# Proposal

## Why

GitHub Actions stopped on 2026-11-20: the account's minutes ran out, so nothing was tested or deployed. Owner decision (2026-11-21): the tests run on the owner's computer, and the server takes main from GitHub and builds it itself. GitHub CI only runs when started by hand.

## What Changes

- **Build on the server** (OPS-16): a timer runs `deploy/pull-deploy.sh` every minute as the user `deploy`. It asks GitHub only for `refs/heads/main` (`git ls-remote`, three tries, SSH on port 443 with pinned host keys); only a new commit is fetched and built once with `scripts/build-release.sh` (server, client checks, `/health` smoke test, Linux agent), then deployed with the same `deploy.sh` (`/ready` after the switch, rollback). A failed build or deploy is written to the journal and not tried again; the running release stays. `install.sh` with `PULL_DEPLOY=1` brings the .NET SDK and Node from the owner's computer; setup makes a read-only GitHub key and prints it.
- **Local gate** (DEL-01): `scripts/ci.sh` runs every step of the GitHub workflow on the owner's computer; a test keeps the two lists equal. The workflow runs only by hand (`workflow_dispatch`).
- **DEL-06**: the server's own build is the main way; the owner's computer over SSH and a manual CI run with deploy on are the fallbacks.
- **DEL-08** (in `l2-site-tunnel`): the Windows agent comes only from a manual CI run; the server's build keeps the running one.

Out of scope: the full .NET tests on the server (they need a test database server; they run in `scripts/ci.sh`); a mirror of the repository inside Iran; a `/ready` check before the switch (it needs the database; the owner chose `/ready` after the switch with rollback).
