## ADDED Requirements

### Requirement: REL-01 Relay owns its schema
Relay SHALL keep bots, memberships and messages in the PostgreSQL schema `relay`, with message content as `jsonb`. No foreign key SHALL point from one schema to another.

#### Scenario: Schema after start
- **WHEN** the server has started on an empty database
- **THEN** the tables `relay.bots`, `relay.memberships` and `relay.messages` exist and `relay.messages.content` is `jsonb`

#### Scenario: No cross-schema foreign key
- **WHEN** the server has started on an empty database
- **THEN** the database has no foreign key between two different schemas

### Requirement: REL-02 Bot username is unique
A bot username SHALL be unique without regard to letter case.

#### Scenario: Same name in another case
- **GIVEN** a bot named `ArcaneBot`
- **WHEN** a bot named `arcanebot` is created
- **THEN** the creation fails and no second bot is stored

### Requirement: REL-03 Token is shown once and stored as a hash
Creating a bot SHALL return a token of the form `<bot id>:<secret>`, where the secret is 35 random characters from letters, digits, `_` and `-`. The database SHALL store only a hash of the secret, never the token.

#### Scenario: Token shape
- **WHEN** a bot is created
- **THEN** the token matches `^<bot id>:[A-Za-z0-9_-]{35}$` and two bots get different secrets

#### Scenario: Only the hash is stored
- **WHEN** a bot is created
- **THEN** no column of its database row contains the secret

### Requirement: REL-04 Bot API needs a valid token
Every Bot API call SHALL carry the token as `Authorization: Bearer <token>`. A missing, malformed or wrong token SHALL get HTTP 401 with error code `unauthorized`, and the call SHALL do nothing.

#### Scenario: No token
- **WHEN** a Bot API call has no `Authorization` header
- **THEN** the response is 401 `unauthorized`

#### Scenario: Wrong secret
- **WHEN** a Bot API call has the id of a real bot and a wrong secret
- **THEN** the response is 401 `unauthorized`

#### Scenario: Valid token
- **WHEN** a Bot API call has the token returned at creation
- **THEN** the call is served as that bot

### Requirement: REL-05 A chat is one user and one bot
The first message of a known user to a bot SHALL create an "active" membership for that user and bot. A message from a user that Identity does not know SHALL be refused.

#### Scenario: First message
- **WHEN** a known user sends the first message to a bot
- **THEN** one "active" membership exists for that user and bot

#### Scenario: Unknown user
- **WHEN** a message arrives with a user id that Identity does not know
- **THEN** it is refused and nothing is stored

### Requirement: REL-06 Sequence number per chat
Every message of a chat, from the user or from the bot, SHALL get the next sequence number of that chat, starting at 1, with no number used twice.

#### Scenario: User and bot take turns
- **WHEN** the user sends a message, the bot answers, and the user sends again
- **THEN** the messages have sequence numbers 1, 2 and 3

#### Scenario: Messages at the same moment
- **WHEN** twenty messages are saved in one chat at the same time
- **THEN** they have the numbers 1 to 20, each once

#### Scenario: Two chats
- **WHEN** two users each send their first message to the same bot
- **THEN** both messages have sequence number 1

### Requirement: REL-07 Get updates
`GET /v1/updates` SHALL return `{"updates":[...]}`. Each update SHALL have `update_id` and a `message` with `id` (the sequence number in the chat), `from` (`id`, `name`, `username` of the user), `text` and `date` (UTC).

#### Scenario: One message waiting
- **GIVEN** user 7 named "Sara" sent "/start"
- **WHEN** the bot calls `GET /v1/updates`
- **THEN** the response is 200 with one update whose message has `from.id` 7, `from.name` "Sara" and `text` "/start"

#### Scenario: Bad parameter
- **WHEN** the bot calls `GET /v1/updates?limit=abc`
- **THEN** the response is 400 with error code `bad_request`

### Requirement: REL-08 Send message
`POST /v1/messages` with `user_id` and `text` SHALL save the bot's message in that chat and return it with its `id`, `user_id`, `text` and `date`. A bot SHALL be able to write only to a user with an "active" membership in that bot; otherwise the response SHALL be HTTP 403 with error code `chat_not_active`.

#### Scenario: Answer a user
- **GIVEN** user 7 has started the bot
- **WHEN** the bot posts `{"user_id":7,"text":"hello"}`
- **THEN** the response is 200 with the message, and the message is in the database as a bot message of that chat

#### Scenario: User never started the bot
- **WHEN** the bot posts a message to a user with no membership in it
- **THEN** the response is 403 `chat_not_active` and nothing is stored

#### Scenario: User stopped the bot
- **GIVEN** the user's membership is "stopped"
- **WHEN** the bot posts a message to that user
- **THEN** the response is 403 `chat_not_active`

#### Scenario: Body is not valid
- **WHEN** the bot posts a body that is not JSON or has no `user_id`
- **THEN** the response is 400 with error code `bad_request`

### Requirement: REL-09 Text length limit
A message text SHALL NOT be empty and SHALL NOT be longer than the setting `Relay:MaxTextLength` (start 4096 characters). This holds for user messages and bot messages.

#### Scenario: Bot sends too long a text
- **WHEN** the bot posts a text one character longer than the limit
- **THEN** the response is 400 with error code `text_too_long` and nothing is stored

#### Scenario: Bot sends an empty text
- **WHEN** the bot posts an empty text
- **THEN** the response is 400 with error code `text_empty`

#### Scenario: User sends too long a text
- **WHEN** a user message is longer than the limit
- **THEN** it is refused and nothing is stored

### Requirement: REL-10 A bot never gets a phone number
Nothing the Bot API returns SHALL contain a user's phone number.

#### Scenario: Update for a user with a phone number
- **GIVEN** a user with phone number `+989121234567` sent a message
- **WHEN** the bot calls `GET /v1/updates`
- **THEN** the response body does not contain that number

### Requirement: REL-11 Errors explain themselves
Every Bot API error SHALL be JSON with `error` (a stable code) and `message` (what went wrong and how to fix it).

#### Scenario: Any error
- **WHEN** a Bot API call fails with 400, 401, 403, 409 or 429
- **THEN** the body has a non-empty `error` and a non-empty `message`
