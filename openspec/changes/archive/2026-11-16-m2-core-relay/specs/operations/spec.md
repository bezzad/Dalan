## MODIFIED Requirements

### Requirement: OPS-02 Readiness endpoint
The server SHALL answer `GET /ready` with HTTP 200 only while it accepts traffic: after its start-up work is done (database schema applied, bot queues reloaded) and before it starts to stop. Otherwise it SHALL answer 503.

#### Scenario: Server is ready
- **WHEN** the server has started and a client calls `GET /ready`
- **THEN** the response is 200

#### Scenario: Server is stopping
- **WHEN** shutdown has started and a client calls `GET /ready`
- **THEN** the response is 503

#### Scenario: Database is not reachable at start
- **GIVEN** the server started and cannot reach the database
- **WHEN** a client calls `GET /ready` and `GET /health`
- **THEN** `/ready` answers 503 and `/health` answers 200

## ADDED Requirements

### Requirement: OPS-07 Schema is applied at start
At start each service SHALL apply its own database migrations, in its own schema. While the database is not reachable the server SHALL keep running and try again every `Dalan:StartupRetrySeconds` (start 5).

#### Scenario: Empty database
- **WHEN** the server starts on an empty database
- **THEN** the schemas `identity` and `relay` exist, each with its own migration history table, and `/ready` answers 200

#### Scenario: Start again
- **WHEN** the server starts on a database that already has the newest schema
- **THEN** it becomes ready and the data is unchanged

### Requirement: OPS-08 No traffic before ready
Until the start-up work is done, every request other than `/health` and `/ready` SHALL get HTTP 503 with error code `not_ready`.

#### Scenario: Call during start-up
- **GIVEN** the server started and cannot reach the database
- **WHEN** a bot calls `GET /v1/updates`
- **THEN** the response is 503 with error code `not_ready`
