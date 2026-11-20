# delivery Specification

## Purpose
Defines the gates every change passes and how a release reaches the server, so bad code does not ship and a failed deploy goes back to the last good release by itself.

## Requirements

### Requirement: DEL-01 CI gate
CI SHALL build the client and the solution, run all server tests and all client tests, type-check the client, and run `openspec validate --strict` on every push and pull request. Any failure SHALL fail the run.

#### Scenario: A test fails
- **WHEN** a pull request has a failing test
- **THEN** the CI run fails

### Requirement: DEL-02 Every rule has a test
CI SHALL fail when a rule ID in a requirement name in `openspec/specs` or in an active change has no test that names that ID. Server tests are under `tests/`, client tests under `client/tests/`.

#### Scenario: Rule without a test
- **GIVEN** a spec has requirement `OPS-99 Something`
- **WHEN** no file under `tests/` or `client/tests/` contains `OPS-99`
- **THEN** the rule check fails and names `OPS-99`

### Requirement: DEL-03 Self-contained release
A release SHALL be one self-contained linux-x64 executable, plus its settings file, that runs on a server with no .NET installed.

#### Scenario: Run on a clean machine
- **WHEN** the release file runs on Linux x64 without .NET
- **THEN** the server starts and `GET /health` answers 200

### Requirement: DEL-04 Deploy with automatic rollback
The deploy SHALL copy the release to a new folder, sending only the parts that differ from the current release, point the current link to it, restart the service, and wait for `/ready`. If `/ready` is not 200 within the configured wait, it SHALL point the link back to the previous release, restart, and exit with an error.

#### Scenario: Healthy release
- **WHEN** the new release answers `/ready` with 200 in time
- **THEN** the current link points to the new release and the deploy exits with success

#### Scenario: Broken release
- **WHEN** the new release does not answer `/ready` with 200 in time
- **THEN** the current link points to the previous release, the service is restarted, and the deploy exits with an error

#### Scenario: Only changes are sent
- **GIVEN** the current release has the same `Dalan.Host` (content and time) as the new one and another `appsettings.json`
- **WHEN** the new release is deployed
- **THEN** its `Dalan.Host` is a hard link to the current one, its `appsettings.json` is the new file, and the current release is not changed

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

### Requirement: DEL-07 One SSH connection, tried again
The deploy SHALL open one SSH connection to the server, trying up to `SSH_TRIES` times (start 5) with `SSH_TIMEOUT` seconds each (start 30), and send every command and upload over that connection. If no try connects, it SHALL exit with an error and change nothing on the server.

#### Scenario: The link stalls twice
- **GIVEN** the first two SSH tries get no answer
- **WHEN** CI runs the deploy
- **THEN** the third try connects, every later call reuses it, and the deploy succeeds

#### Scenario: No connection
- **GIVEN** no SSH try connects
- **WHEN** CI runs the deploy
- **THEN** the deploy exits with an error and the current link is not changed
