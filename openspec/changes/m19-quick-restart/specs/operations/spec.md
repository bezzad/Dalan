## MODIFIED Requirements

### Requirement: OPS-03 Graceful shutdown
On a stop signal the server SHALL stop taking new connections, let running requests finish, and exit within the configured shutdown timeout (`Dalan:ShutdownTimeoutSeconds`). Long-lived connections (the live stream, a bot long poll, a site tunnel, an agent long poll) and background workers SHALL end at once on the stop signal, so that a server with no other running request exits within `Dalan:StopGraceSeconds` (start 5). A site tunnel SHALL close with WebSocket status 1001.

#### Scenario: Request in flight during stop
- **GIVEN** a request is running
- **WHEN** the server gets a stop signal
- **THEN** the request completes and the server exits within the shutdown timeout

#### Scenario: Long-lived connections do not hold the stop
- **GIVEN** a user has an open live stream, a bot waits in a long poll, and an agent has an open site tunnel
- **WHEN** the server gets a stop signal
- **THEN** the stream ends, the long poll answers, the tunnel closes with status 1001, and the server exits within `Dalan:StopGraceSeconds`

#### Scenario: Deploy gap
- **WHEN** a deploy restarts the server while bots and users are connected
- **THEN** the server refuses connections for at most `Dalan:StopGraceSeconds` plus its start time, not for `Dalan:ShutdownTimeoutSeconds`
