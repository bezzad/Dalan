# delivery Specification

## Purpose
Defines the gates every change passes and how a release reaches the server, so bad code does not ship and a failed deploy goes back to the last good release by itself.

## Requirements

### Requirement: DEL-01 CI gate
The gate SHALL be `scripts/ci.sh` on the owner's computer, with every step of the GitHub workflow: build, all server and client tests, client type-check, release smoke test, linux-x64 agent, `openspec validate --all --strict`, rule check, script tests, and the Docker image test when Docker is there. It SHALL print PASS or FAIL per step and the total time, and exit non-zero at the first failure or missing tool. The workflow SHALL run only by hand, with the same steps.

#### Scenario: A test fails
- **WHEN** a test fails during `scripts/ci.sh`
- **THEN** it prints FAIL for that step and exits with a non-zero code

#### Scenario: A tool is missing
- **GIVEN** `caddy` is not on the PATH
- **WHEN** the owner runs `scripts/ci.sh`
- **THEN** it fails at once and names `caddy`

#### Scenario: The two gates drift apart
- **WHEN** a test script or test project is in the workflow and not in `scripts/ci.sh` (or the other way)
- **THEN** the workflow test fails

#### Scenario: A push
- **WHEN** a commit is pushed or a pull request is opened
- **THEN** no GitHub CI machine starts

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

### Requirement: DEL-06 How a release reaches the server
The deploy script SHALL deploy a release folder it is given, and SHALL NOT itself reach GitHub or any foreign host. The main way a release reaches the server SHALL be the server's own build of main (OPS-16), which reaches only GitHub. The fallbacks SHALL be the owner's computer over SSH, and a CI run started by hand with deploy on.

#### Scenario: Server has no internet
- **GIVEN** the server cannot reach GitHub
- **WHEN** the owner's computer or a manual CI run deploys over SSH
- **THEN** the deploy succeeds using only the SSH connection

#### Scenario: A merge to main
- **WHEN** a pull request is merged to main
- **THEN** GitHub CI does not deploy; the server builds the new main itself

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

### Requirement: DEL-08 The agent for every system ships with the server
Every release SHALL have the agent, built with Native AOT, in its `agent` folder, which is `Sites:AgentFolder` (AGT-10): `dalan-linux-x64` from each build (the server's own build, `scripts/ci.sh` and CI), and `dalan-windows-x64.exe`, `dalan-macos-arm64` and `dalan-macos-x64` from a CI run started by hand (each on its own system; Native AOT does not cross systems). A build that cannot make a file SHALL keep the one of the running release (OPS-16).

#### Scenario: Deploy
- **WHEN** a manual CI run deploys
- **THEN** the new release has `agent/dalan-linux-x64`, `agent/dalan-windows-x64.exe`, `agent/dalan-macos-arm64` and `agent/dalan-macos-x64`

#### Scenario: The server's own build
- **WHEN** the server builds and deploys a new main
- **THEN** the new release has a new `agent/dalan-linux-x64` and the running release's Windows and macOS files

### Requirement: DEL-09 Installs take nothing from a foreign host
`install.sh`, `install.ps1` and `dalan.mjs` (the files a user runs) SHALL name no host except the Dalan address given to them (`__DALAN_URL__`, `DALAN_URL`): no GitHub, npm, NuGet, CDN or any other address. The agent SHALL have no default server except `dalanet.ir`. The server's own setup (`deploy/install.sh`) MAY reach foreign hosts once, and the milestone summary SHALL list them.

#### Scenario: User scripts
- **WHEN** the three files are scanned for `http://` and `https://` addresses
- **THEN** none is found except the `__DALAN_URL__` placeholder
