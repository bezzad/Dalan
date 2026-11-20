# Spec Delta

## ADDED Requirements

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
