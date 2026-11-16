## MODIFIED Requirements

### Requirement: DEL-01 CI gate
CI SHALL build the client and the solution, run all server tests and all client tests, type-check the client, and run `openspec validate --strict` on every push and pull request. Any failure SHALL fail the run.

#### Scenario: A test fails
- **WHEN** a pull request has a failing test
- **THEN** the CI run fails
