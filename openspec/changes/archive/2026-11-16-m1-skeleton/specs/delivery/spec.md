# Spec Delta

## Purpose

Defines the gates every change passes and how a release reaches the server, so bad code does not ship and a failed deploy goes back to the last good release by itself.

## ADDED Requirements

### Requirement: DEL-01 CI gate
CI SHALL build the solution, run all tests, type-check the client, and run `openspec validate --strict` on every push and pull request. Any failure SHALL fail the run.

#### Scenario: A test fails
- **WHEN** a pull request has a failing test
- **THEN** the CI run fails

### Requirement: DEL-02 Every rule has a test
CI SHALL fail when a rule ID in a requirement name in `openspec/specs` or in an active change has no test that names that ID.

#### Scenario: Rule without a test
- **GIVEN** a spec has requirement `OPS-99 Something`
- **WHEN** no file under `tests/` contains `OPS-99`
- **THEN** the rule check fails and names `OPS-99`

### Requirement: DEL-03 Self-contained release
A release SHALL be one self-contained linux-x64 executable, plus its settings file, that runs on a server with no .NET installed.

#### Scenario: Run on a clean machine
- **WHEN** the release file runs on Linux x64 without .NET
- **THEN** the server starts and `GET /health` answers 200

### Requirement: DEL-04 Deploy with automatic rollback
The deploy SHALL copy the release to a new folder, point the current link to it, restart the service, and wait for `/ready`. If `/ready` is not 200 within the configured wait, it SHALL point the link back to the previous release, restart, and exit with an error.

#### Scenario: Healthy release
- **WHEN** the new release answers `/ready` with 200 in time
- **THEN** the current link points to the new release and the deploy exits with success

#### Scenario: Broken release
- **WHEN** the new release does not answer `/ready` with 200 in time
- **THEN** the current link points to the previous release, the service is restarted, and the deploy exits with an error

### Requirement: DEL-05 Keep recent releases
After a successful deploy, the server SHALL keep the configured number of newest releases and delete older ones. The current release MUST NOT be deleted.

#### Scenario: More releases than the limit
- **GIVEN** the keep count is 5 and the server has 7 releases
- **WHEN** a deploy succeeds
- **THEN** the 5 newest releases remain, including the current one

### Requirement: DEL-06 Server never pulls
The deploy SHALL push files from CI to the server over SSH. The server MUST NOT need access to GitHub or any foreign host to deploy.

#### Scenario: Server has no internet
- **GIVEN** the server cannot reach GitHub
- **WHEN** CI runs the deploy
- **THEN** the deploy succeeds using only the SSH connection
