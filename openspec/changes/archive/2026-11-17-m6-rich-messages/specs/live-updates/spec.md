## MODIFIED Requirements

### Requirement: LIVE-02 New messages arrive live
Every new or changed message of a chat (sent, edited, deleted, pinned or unpinned), from the bot or from the user, SHALL be sent to every open stream of that chat's user as an event named `message`, with JSON data `bot_id` and `message` (the same shape as in REL-16, with its new `version`).

#### Scenario: Bot answers
- **GIVEN** a user with an open stream who started a bot
- **WHEN** the bot sends "hello"
- **THEN** the stream gets a `message` event with that bot's id, `from` "bot" and `text` "hello"

#### Scenario: Same user on a second device
- **GIVEN** a user with an open stream
- **WHEN** the same user sends a message from another connection
- **THEN** the stream gets a `message` event with `from` "user"

#### Scenario: Bot edits
- **GIVEN** a user with an open stream and bot message 2
- **WHEN** the bot edits message 2
- **THEN** the stream gets a `message` event for message 2 with the new text and a higher `version`

### Requirement: LIVE-04 Events go through the outbox
A `MessageChanged` event SHALL be written to the table `relay.outbox` in the same transaction as the change of its message. A worker SHALL read the table, pass each event to the notifier and remove it. An event that was not passed on (for example after a crash) SHALL be passed on later; the worker looks at least every `Relay:OutboxPollSeconds` (start 5).

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

## ADDED Requirements

### Requirement: LIVE-09 Answers to button presses
A bot's answer to a press (REL-22) SHALL be sent to every open stream of the user who pressed as an event named `answer`, with JSON data `bot_id`, `callback_id`, `text` (may be missing) and `show_alert`. It is not stored: a user with no open stream does not get it.

#### Scenario: Answer
- **GIVEN** the user pressed a button and has an open stream
- **WHEN** the bot answers with "Saved"
- **THEN** the stream gets an `answer` event with `text` "Saved" and the press id
