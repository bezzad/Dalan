## ADDED Requirements

### Requirement: LIVE-01 Event stream
`GET /api/events` SHALL answer a logged-in user with a Server-Sent Events stream (`Content-Type: text/event-stream`) that stays open. It is a Client API call (REL-12).

#### Scenario: Open the stream
- **WHEN** a logged-in user calls `GET /api/events`
- **THEN** the response is 200 with content type `text/event-stream` and stays open

### Requirement: LIVE-02 New messages arrive live
Every new message of a chat, from the bot or from the user, SHALL be sent to every open stream of that chat's user as an event named `message`, with JSON data `bot_id` and `message` (the same shape as in REL-16).

#### Scenario: Bot answers
- **GIVEN** a user with an open stream who started a bot
- **WHEN** the bot sends "hello"
- **THEN** the stream gets a `message` event with that bot's id, `from` "bot" and `text` "hello"

#### Scenario: Same user on a second device
- **GIVEN** a user with an open stream
- **WHEN** the same user sends a message from another connection
- **THEN** the stream gets a `message` event with `from` "user"

### Requirement: LIVE-03 Only the chat's user gets the event
A message SHALL NOT be sent to a stream of any other user.

#### Scenario: Two users of one bot
- **GIVEN** users A and B both have an open stream and both started the same bot
- **WHEN** the bot sends a message to A
- **THEN** A's stream gets it and B's stream gets nothing

### Requirement: LIVE-04 Events go through the outbox
A `MessageCreated` event SHALL be written to the table `relay.outbox` in the same transaction as its message. A worker SHALL read the table, pass each event to the notifier and remove it. An event that was not passed on (for example after a crash) SHALL be passed on later; the worker looks at least every `Relay:OutboxPollSeconds` (start 5).

#### Scenario: Message and event are saved together
- **GIVEN** the worker is not running
- **WHEN** a message is saved
- **THEN** one outbox row for that message exists

#### Scenario: Event is removed after it is passed on
- **WHEN** the worker has passed an event to the notifier
- **THEN** its outbox row is gone

#### Scenario: Left-over event
- **GIVEN** an outbox row that nobody announced, and its user has an open stream
- **WHEN** the poll time passes
- **THEN** the stream gets the event

### Requirement: LIVE-05 Heartbeat
An open stream SHALL get a comment line every `Relay:StreamHeartbeatSeconds` (start 20), so proxies keep it open and a dead connection is noticed.

#### Scenario: Quiet stream
- **GIVEN** an open stream with no new messages
- **WHEN** the heartbeat time passes
- **THEN** the stream gets a comment line

### Requirement: LIVE-06 Stream limit per user
A user SHALL have at most `Relay:MaxStreamsPerUser` open streams (start 5). When a new stream goes over the limit, the oldest stream of that user SHALL be closed.

#### Scenario: One stream too many
- **GIVEN** the limit is 2 and a user has 2 open streams
- **WHEN** the user opens a third
- **THEN** the first stream ends and the other two stay open

### Requirement: LIVE-07 Stream ends with the session and with the server
A stream SHALL end when its session expires and when the server starts to stop.

#### Scenario: Session expires
- **GIVEN** an open stream
- **WHEN** the session's lifetime passes
- **THEN** the stream ends

#### Scenario: Server stops
- **GIVEN** an open stream
- **WHEN** the server starts to stop
- **THEN** the stream ends without waiting

### Requirement: LIVE-08 A slow reader is cut off
Each stream SHALL buffer at most `Relay:StreamBufferSize` events (start 100). When the buffer is full the stream SHALL be closed, so the client reconnects and syncs, and other users are not slowed down.

#### Scenario: Reader does not keep up
- **GIVEN** a subscription with buffer size 2 that is not read
- **WHEN** 3 events are published for its user
- **THEN** the subscription is closed
