# operations Specification

## Purpose
Defines how the running Dalan server reports its health, starts, stops, logs and reads settings, so it can run under systemd or any container platform.

## Requirements

### Requirement: OPS-01 Liveness endpoint
The server SHALL answer `GET /health` with HTTP 200 and a JSON body with `status` set to `ok` and `version` set to the release version. It MUST NOT call the database or any other dependency.

#### Scenario: Server is running
- **WHEN** a client calls `GET /health`
- **THEN** the response is 200 with JSON `{"status":"ok","version":"<release version>"}`

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

### Requirement: OPS-03 Graceful shutdown
On a stop signal the server SHALL stop taking new connections, let running requests finish, and exit within the configured shutdown timeout.

#### Scenario: Request in flight during stop
- **GIVEN** a request is running
- **WHEN** the server gets a stop signal
- **THEN** the request completes and the server exits within the shutdown timeout

### Requirement: OPS-04 Structured logs on stdout
The server SHALL write logs to stdout as one JSON object per line, with time, level, category and message.

#### Scenario: A log line is written
- **WHEN** the server logs a message
- **THEN** stdout gets one line that parses as JSON with `Timestamp`, `LogLevel`, `Category` and `Message`

### Requirement: OPS-05 Settings from files and environment
The server SHALL read settings from its settings file and from environment variables, and an environment variable SHALL win over the file. Secrets MUST NOT be stored in the repository.

#### Scenario: Environment overrides file
- **GIVEN** the settings file sets `Dalan:Version` to `dev`
- **WHEN** the environment variable `Dalan__Version` is `env`
- **THEN** `GET /health` reports version `env`

### Requirement: OPS-06 Probes are private
`/health` and `/ready` SHALL NOT be reachable through the public front proxy; only the server itself and the deploy script on it use them.

#### Scenario: Public call to a probe
- **WHEN** a client on the internet calls `https://<site domain>/health`
- **THEN** the front proxy answers 404 and does not forward the call

### Requirement: OPS-07 Schema is applied at start
At start each service SHALL apply its own database migrations, in its own schema. While the database is not reachable the server SHALL keep running and try again every `Dalan:StartupRetrySeconds` (start 5).

#### Scenario: Empty database
- **WHEN** the server starts on an empty database
- **THEN** the schemas `identity`, `relay` and `admin` exist, each with its own migration history table, and `/ready` answers 200

#### Scenario: Start again
- **WHEN** the server starts on a database that already has the newest schema
- **THEN** it becomes ready and the data is unchanged

### Requirement: OPS-08 No traffic before ready
Until the start-up work is done, every request other than `/health` and `/ready` SHALL get HTTP 503 with error code `not_ready`.

#### Scenario: Call during start-up
- **GIVEN** the server started and cannot reach the database
- **WHEN** a bot calls `GET /v1/updates`
- **THEN** the response is 503 with error code `not_ready`
