# Tasks

- [x] 1.1 `deploy/pull-deploy.sh`, `scripts/build-release.sh`, `dalan-pull` units, setup and install with `PULL_DEPLOY=1`
- [x] 1.2 CI: no run on drafts, tests on two machines
- [x] 1.3 docs/SERVER.md
- [ ] 1.4 First run on the server: the owner runs `install.sh` with `PULL_DEPLOY=1` and adds the deploy key on GitHub

## 2. Tests on the owner's computer (2026-11-21)

- [x] 2.1 DEL-01: `scripts/ci.sh` with the steps of the workflow; `tests/delivery/ci_workflow_test.sh` keeps the two equal
- [x] 2.2 DEL-01, DEL-06, DEL-08: the workflow runs only by hand; its deploy only with deploy on
- [x] 2.3 OPS-16: timer every minute, `git ls-remote` of `refs/heads/main` only, a failed deploy not tried again, GitHub host keys pinned
- [x] 2.4 CLAUDE.md rules 6 and 11, docs/SERVER.md (deploy key commands), docs/TESTING.md
- [x] 2.5 `scripts/ci.sh` green on the owner's computer, then merge
