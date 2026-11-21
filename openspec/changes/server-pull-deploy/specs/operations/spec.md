# Spec Delta

## ADDED Requirements

### Requirement: OPS-16 The server builds main itself
With `PULL_DEPLOY=1`, setup SHALL install the .NET SDK and Node brought by `install.sh`, a read-only GitHub key (`ssh.github.com` port 443, pinned host keys) and `dalan-pull.timer`, every minute. Each run SHALL ask GitHub only for `refs/heads/main`, and build and deploy each new commit once: `/health` before the switch, `/ready` after it with rollback. A failed build or deploy SHALL keep the running release and its Windows agent file, and SHALL NOT be tried again.

#### Scenario: New main
- **GIVEN** a new commit on main
- **WHEN** the timer runs twice
- **THEN** the commit is built and deployed once, and the Windows agent file stays

#### Scenario: Nothing new
- **GIVEN** main is the running release
- **WHEN** the timer runs
- **THEN** it asks GitHub only for `refs/heads/main` and fetches nothing

#### Scenario: Another branch
- **WHEN** a commit is pushed to a branch other than main
- **THEN** the server never builds it

#### Scenario: Broken build
- **WHEN** the build of the new commit fails
- **THEN** the running release stays, and the next run does not build it again

#### Scenario: GitHub does not answer
- **WHEN** `git ls-remote` fails three times
- **THEN** the run fails, the running release stays, and the next run tries again

#### Scenario: Not ready after the switch
- **WHEN** the deploy of the new commit rolls back
- **THEN** the reason is in the journal, and the next run does not deploy it again

#### Scenario: Two runs at once
- **GIVEN** a run is still building
- **WHEN** the timer starts another run
- **THEN** the second run stops at once and changes nothing
