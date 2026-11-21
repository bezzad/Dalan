# Spec Delta

## RENAMED Requirements
- FROM: `### Requirement: DEL-06 Server never pulls`
- TO: `### Requirement: DEL-06 How a release reaches the server`

## MODIFIED Requirements

### Requirement: DEL-01 CI gate
The gate SHALL be `scripts/ci.sh` on the owner's computer. It SHALL build the client and the solution, run all server tests and all client tests, type-check the client, publish the release and smoke-test it, build the linux-x64 agent, run `openspec validate --all --strict`, the rule check and every deploy, delivery and proxy script test, and the Docker image test when Docker is there. It SHALL print PASS or FAIL for each step and the total time, and stop with a non-zero exit at the first failure. A missing tool (other than Docker) SHALL fail the run with its name, never skip a step. The GitHub workflow SHALL run the same steps, only when started by hand; a test SHALL fail when a step or test project is in one of the two and not in the other.

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

### Requirement: DEL-06 How a release reaches the server
The deploy script SHALL deploy a release folder it is given, and SHALL NOT itself reach GitHub or any foreign host. The main way a release reaches the server SHALL be the server's own build of main (OPS-16), which reaches only GitHub. The fallbacks SHALL be the owner's computer over SSH, and a CI run started by hand with deploy on.

#### Scenario: Server has no internet
- **GIVEN** the server cannot reach GitHub
- **WHEN** the owner's computer or a manual CI run deploys over SSH
- **THEN** the deploy succeeds using only the SSH connection

#### Scenario: A merge to main
- **WHEN** a pull request is merged to main
- **THEN** GitHub CI does not deploy; the server builds the new main itself
