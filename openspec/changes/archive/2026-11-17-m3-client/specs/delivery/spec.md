## MODIFIED Requirements

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
