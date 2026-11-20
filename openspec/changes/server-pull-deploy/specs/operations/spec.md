# Spec Delta

## ADDED Requirements

### Requirement: OPS-16 The server builds main itself
With `PULL_DEPLOY=1`, setup SHALL install the .NET SDK and Node from `install.sh`, a read-only GitHub key (port 443) and `dalan-pull.timer`. Each run SHALL fetch main (three tries), build a new commit once with `scripts/build-release.sh` and deploy it with `deploy.sh`. A failed build SHALL NOT be tried again, and a failed build or fetch SHALL keep the running release and its Windows agent file.

#### Scenario: New main
- **GIVEN** a new commit on main
- **WHEN** the timer runs twice
- **THEN** the commit is built and deployed once, and the Windows agent file stays

#### Scenario: Broken build
- **WHEN** the build of the new commit fails
- **THEN** the running release stays, and the next run does not build it again
