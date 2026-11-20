# Spec Delta

## MODIFIED Requirements

### Requirement: DEL-01 CI gate
CI SHALL build the client and the solution, run all server tests and all client tests, type-check the client, and run `openspec validate --strict` on every push to main and on every pull request that is not a draft (also when a draft is marked ready). Any failure SHALL fail the run.

#### Scenario: A test fails
- **WHEN** a pull request has a failing test
- **THEN** the CI run fails

#### Scenario: Draft
- **WHEN** a commit is pushed to a draft pull request
- **THEN** no CI machine starts

### Requirement: DEL-06 Server never pulls
The deploy script SHALL deploy a release folder it is given, and SHALL NOT itself reach GitHub or any foreign host. A release reaches the server by a push from CI over SSH, by the owner's computer over SSH, or by the server's own build of main (OPS-16), which reaches only GitHub.

#### Scenario: Server has no internet
- **GIVEN** the server cannot reach GitHub
- **WHEN** CI runs the deploy
- **THEN** the deploy succeeds using only the SSH connection
