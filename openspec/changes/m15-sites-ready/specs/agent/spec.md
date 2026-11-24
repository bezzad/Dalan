# Spec Delta

## MODIFIED Requirements

### Requirement: AGT-07 Forward to the local app
The agent SHALL send each request to `http://localhost:<port>` of its site with `Host: localhost:<port>` and the other headers, and return the status, headers and body. When the local app does not answer, the visitor SHALL get 502 with a page that names the port. The body SHALL go back in one message when it ends quickly, else in parts (AGT-12).

#### Scenario: Local app stopped
- **GIVEN** nothing listens on port 3000
- **WHEN** a visitor opens the site of port 3000
- **THEN** the visitor gets 502 and the page says port 3000 does not answer

#### Scenario: Small answer
- **WHEN** the app answers with a short body at once
- **THEN** the agent sends it in one message

## ADDED Requirements

### Requirement: AGT-12 Stream the answer
The agent SHALL send a part when it has `stream_chunk_kb` bytes, when the answer ends, or when no new byte came for 20 ms. Over WebSocket it SHALL keep at most `stream_window` parts of one answer without `ack`. On `cancel`, or 410 for a part sent by long polling, it SHALL stop reading that local answer.

#### Scenario: Big file
- **WHEN** a visitor downloads a file larger than one part
- **THEN** the file comes in parts and arrives whole

#### Scenario: Cancel
- **WHEN** the server sends `cancel` for a running stream
- **THEN** the agent closes its connection to the local app for it

### Requirement: AGT-13 Stop ends the session
On Ctrl+C, or SIGTERM (a service stop or a shutdown), the agent SHALL end its session on the server (at most 5 seconds) before it exits.

#### Scenario: Ctrl+C
- **GIVEN** a running agent
- **WHEN** the user presses Ctrl+C
- **THEN** the server ends the session and visitors get the offline page at once

### Requirement: AGT-14 Run as a service
`dalan service install` SHALL make the saved sites run at start-up: a systemd user unit on Linux (with linger, or a note when linger fails) and a launchd agent on macOS, both started again after a crash; on Windows the user's "Run" key (no admin needed) with `dalan start --background`, started at once. `dalan service uninstall` SHALL stop and remove it. Install SHALL refuse with a clear message when there is no key or no saved site.

#### Scenario: Linux
- **WHEN** a user with a key and a saved site runs `dalan service install` on Linux
- **THEN** a unit that runs `<agent file> start` with `Restart=always` is written, and enabled and started for the user

#### Scenario: Nothing saved
- **WHEN** a user with no saved site runs `dalan service install`
- **THEN** the agent stops with a message that says to run `dalan http <port> --name <name>` first

### Requirement: AGT-15 Status
`dalan status` SHALL print whether the service is installed and running, then each site of the key's builder with its address and online or offline.

#### Scenario: One site online
- **GIVEN** the site `levelwise` online
- **WHEN** the user runs `dalan status`
- **THEN** the output has `levelwise`, its address and `online`
