# Spec Delta

## MODIFIED Requirements

### Requirement: SITE-08 Public routing
A request whose host is `<name>.<Sites:Domain>` SHALL go to that site's session and nowhere else in Dalan. The host match SHALL ignore a port, and `Sites:Domain` MAY carry a port for local work (`localhost:5000`), which the site address then shows. Hop-by-hop headers SHALL be dropped both ways. The visitor's own `X-Forwarded-*` headers SHALL be replaced by the real client address, the scheme and the host. The site's answer SHALL NOT get Dalan's security headers. An answer with status 1xx, 204 or 304, or to a `HEAD` request, SHALL go out with no body. With `Sites:Domain` empty, no host is a site.

#### Scenario: Visitor headers
- **WHEN** a visitor from `5.6.7.8` sends `X-Forwarded-For: 1.1.1.1` and `Connection: x-secret` to a site
- **THEN** the agent gets `X-Forwarded-For: 5.6.7.8` and no `Connection` header

#### Scenario: Site paths are the site's
- **WHEN** a visitor asks a site for `/api/me`
- **THEN** the request goes to the agent, not to Dalan's own `/api/me`

#### Scenario: No Dalan headers on a site answer
- **WHEN** a site answers a visitor
- **THEN** the answer has no `Content-Security-Policy` that Dalan added

#### Scenario: Not modified
- **WHEN** the app answers 304 or 204 (even with a body)
- **THEN** the visitor gets that status with no body, and the next request on the same connection works

#### Scenario: Domain with a port
- **GIVEN** `Sites:Domain` `localhost:5000`
- **WHEN** a visitor opens `levelwise.localhost:5000`
- **THEN** the request goes to the site `levelwise`, and its address is `http://levelwise.localhost:5000`

### Requirement: SITE-10 Request timeout
A request with no first answer message after `Sites:RequestTimeoutSeconds` SHALL get 504 (`site_timeout`). A streamed answer that has started SHALL NOT be cut by this time.

#### Scenario: Slow home app
- **WHEN** the agent takes a request and does not answer in time
- **THEN** the visitor gets 504

#### Scenario: Long stream
- **GIVEN** an answer whose first part came in time
- **WHEN** its parts keep coming for longer than the timeout
- **THEN** the visitor gets every part

### Requirement: SITE-11 Size limits
A visitor request body over `Sites:MaxBodyMb` SHALL get 413 and never reach the agent. One answer message over `Sites:MaxBodyMb` SHALL be refused and the visitor SHALL get 502. A streamed answer over `Sites:MaxAnswerMb` in total SHALL be stopped.

#### Scenario: Big upload
- **WHEN** a visitor posts a body over the limit
- **THEN** the answer is 413 and the agent gets nothing

#### Scenario: Endless answer
- **WHEN** a streamed answer passes `Sites:MaxAnswerMb`
- **THEN** the visitor's connection is closed and the agent is told to stop

### Requirement: SITE-12 Waiting requests per visitor address
At most `Sites:MaxPendingPerAddress` (start 200) requests from one client address SHALL be at the agent at once. More SHALL wait in a line, in order, up to `Sites:MaxQueuedPerAddress` (start 1000) requests, each for at most `Sites:RequestTimeoutSeconds`. A request beyond the line, or one that waited too long, SHALL get 429.

#### Scenario: A burst waits
- **GIVEN** `Sites:MaxPendingPerAddress` requests from one address at the agent
- **WHEN** that address sends one more
- **THEN** it waits, and goes to the agent when one of the others ends; another address is served at once

#### Scenario: Too many at once
- **GIVEN** `Sites:MaxPendingPerAddress` requests from one address at the agent and its line full
- **WHEN** that address sends one more
- **THEN** it gets 429 while another address is still served

## ADDED Requirements

### Requirement: SITE-22 Streamed answers
The agent SHALL be able to send an answer in parts (`more: true` until the last). The server SHALL write and flush each part to the visitor at once, and SHALL give the agent `Sites:StreamChunkKb` and `Sites:StreamWindow` when it opens a session. At most the window of parts SHALL be on the way per answer; more SHALL end that answer. When the visitor leaves, the agent SHALL be told to stop.

#### Scenario: Server-Sent Events
- **GIVEN** an app that sends one event, waits, then sends another
- **WHEN** a visitor opens the event stream through Dalan
- **THEN** the visitor gets the first event before the second is sent, and over WebSocket each written part gets an `ack`

#### Scenario: Visitor leaves
- **WHEN** a visitor closes a stream that is still running
- **THEN** over WebSocket the agent gets `cancel` for it and stops reading the app

#### Scenario: Stream by long polling
- **GIVEN** an agent on long polling
- **WHEN** it streams an answer
- **THEN** each part's `POST` returns after the part was written, the visitor gets every part in order, and a part for a request that ended gets 410 `request_gone`

### Requirement: SITE-23 A stopped agent is offline at once
`DELETE /api/tunnel/sessions/{session}` SHALL end the caller's session. When the WebSocket of a session closes, the server SHALL end that session. Visitors waiting on an ended session SHALL get 502 `site_offline` at once.

#### Scenario: Ctrl+C
- **GIVEN** a site online
- **WHEN** its agent ends the session
- **THEN** the next visitor gets 502 `site_offline` at once, and the list shows the site offline

#### Scenario: WebSocket closed
- **WHEN** the agent's WebSocket closes
- **THEN** the session ends and a visitor gets 502 at once

### Requirement: SITE-24 Reserve a name
`POST /api/sites` with `{"name": "<name>"}` SHALL claim the name for the calling builder by the rules of SITE-02 and SITE-03, with no agent: 201 with the site (offline), 200 when the caller already owns it, 400 `bad_name`, 409 `name_taken` or 409 `site_limit`.

#### Scenario: Reserve
- **WHEN** a builder reserves `levelwise`
- **THEN** the answer is 201, the list has `levelwise` offline, and another builder gets 409 `name_taken` for it

#### Scenario: Taken
- **GIVEN** `shop` belongs to another builder
- **WHEN** a builder reserves `shop`
- **THEN** the answer is 409 `name_taken`
