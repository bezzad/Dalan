# Spec Delta

## Purpose

Defines how the running Dalan server reports its health, starts, stops, logs and reads settings, so it can run under systemd or any container platform.

## ADDED Requirements

### Requirement: OPS-01 Liveness endpoint
The server SHALL answer `GET /health` with HTTP 200 and a JSON body with `status` set to `ok` and `version` set to the release version. It MUST NOT call the database or any other dependency.

#### Scenario: Server is running
- **WHEN** a client calls `GET /health`
- **THEN** the response is 200 with JSON `{"status":"ok","version":"<release version>"}`

### Requirement: OPS-02 Readiness endpoint
The server SHALL answer `GET /ready` with HTTP 200 only while it accepts traffic. While the server is stopping, it SHALL answer 503.

#### Scenario: Server is ready
- **WHEN** the server has started and a client calls `GET /ready`
- **THEN** the response is 200

#### Scenario: Server is stopping
- **WHEN** shutdown has started and a client calls `GET /ready`
- **THEN** the response is 503

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
