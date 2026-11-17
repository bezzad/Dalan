# relay Specification

## Purpose
The core of Dalan: bots and their tokens, chats between one user and one bot, messages, the Bot API that a bot at home calls and the Client API that the web app calls.

## Requirements

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
Every Bot API and Client API error SHALL be JSON with `error` (a stable code) and `message` (what went wrong and how to fix it).

#### Scenario: Any error
- **WHEN** a Bot API call fails with 400, 401, 403, 409 or 429
- **THEN** the body has a non-empty `error` and a non-empty `message`

#### Scenario: Any Client API error
- **WHEN** a Client API call fails with 400, 401, 404 or 429
- **THEN** the body has a non-empty `error` and a non-empty `message`

### Requirement: REL-12 Client API needs a session
Every Client API call (paths under `/api/bots`, `/api/events` and `/api/settings`) SHALL need a valid session (AUTH-03) and SHALL act only as the user of that session. Without one the response SHALL be HTTP 401 with error code `unauthorized`, and the call SHALL do nothing.

#### Scenario: No session
- **WHEN** a Client API call has no session cookie
- **THEN** the response is 401 `unauthorized`

#### Scenario: With a session
- **WHEN** a logged-in user calls the Client API
- **THEN** the call is served as that user

### Requirement: REL-13 Find a bot by username
`GET /api/bots/by-username/{username}` SHALL return the bot's `id`, `username`, `name` and `started` (whether this user has a chat with it). The username SHALL match without regard to letter case. An unknown username SHALL get HTTP 404 with error code `bot_not_found`.

#### Scenario: Known bot, not started
- **GIVEN** a bot named `ArcaneBot` that the user never wrote to
- **WHEN** the user calls `GET /api/bots/by-username/arcanebot`
- **THEN** the response is 200 with `username` "ArcaneBot" and `started` false

#### Scenario: Known bot, started
- **GIVEN** the user sent a message to the bot
- **WHEN** the user asks for the bot by username
- **THEN** `started` is true

#### Scenario: Unknown bot
- **WHEN** the user asks for a username that no bot has
- **THEN** the response is 404 `bot_not_found`

### Requirement: REL-14 List my bots
`GET /api/bots` SHALL return `{"bots":[...]}` with the `id`, `username` and `name` of every bot this user has a chat with, newest chat first, and no other bot.

#### Scenario: Two users, two bots
- **GIVEN** user A wrote to bot 1 and user B wrote to bot 2
- **WHEN** user A calls `GET /api/bots`
- **THEN** the list holds bot 1 only

#### Scenario: No chat yet
- **WHEN** a new user calls `GET /api/bots`
- **THEN** the list is empty

### Requirement: REL-15 A user sends a message
`POST /api/bots/{bot id}/messages` with `text` SHALL save the user's message in the chat of this user and that bot, put it in the bot's queue, and return it with `id` (the sequence number in the chat), `from` ("user"), `text` and `date`. The text rules of REL-09 apply, with error codes `text_empty` and `text_too_long`. An unknown bot SHALL get HTTP 404 `bot_not_found`.

#### Scenario: Start a bot
- **WHEN** a user posts `{"text":"/start"}` to a bot for the first time
- **THEN** the response is 200 with `id` 1 and `from` "user", and the bot's next get-updates call returns "/start" from that user

#### Scenario: Unknown bot
- **WHEN** a user posts a message to a bot id that does not exist
- **THEN** the response is 404 `bot_not_found` and nothing is stored

#### Scenario: Text too long or empty
- **WHEN** a user posts an empty text or one longer than the limit
- **THEN** the response is 400 with `text_empty` or `text_too_long` and nothing is stored

#### Scenario: Body is not valid
- **WHEN** a user posts a body that is not JSON or has no `text`
- **THEN** the response is 400 with error code `bad_request`

### Requirement: REL-16 History and sync
`GET /api/bots/{bot id}/messages` SHALL return `{"messages":[...]}` from the chat of this user and that bot only, in rising `id` order. Each message has `id`, `from` ("user" or "bot"), `text`, `date` and, when it carries one, `reply_keyboard`. The parameters `after`, `before` and `limit` choose the page. The page size is never above `Relay:MaxMessagesPerSync` (start 100).

#### Scenario: Open a chat
- **GIVEN** a chat with messages 1 to 5
- **WHEN** the user calls it with `limit=3` and no other parameter
- **THEN** the newest messages 3, 4 and 5 are returned in that order

#### Scenario: Limit not given or too big
- **GIVEN** the maximum is 2 and the chat has 5 messages
- **WHEN** the user calls it with no `limit`, or with `limit=50`
- **THEN** 2 messages are returned

#### Scenario: Sync after a number
- **GIVEN** a chat with messages 1 to 5
- **WHEN** the user calls it with `after=2&limit=2`
- **THEN** the messages 3 and 4 are returned (the oldest after 2)

#### Scenario: Older messages
- **GIVEN** a chat with messages 1 to 5
- **WHEN** the user calls it with `before=4&limit=2`
- **THEN** the messages 2 and 3 are returned (the newest before 4)

#### Scenario: Another user's chat
- **GIVEN** user A and user B each have a chat with the same bot
- **WHEN** user A reads the messages of that bot
- **THEN** no message of user B is returned

#### Scenario: Bad parameter
- **WHEN** the user calls it with `after=abc`
- **THEN** the response is 400 with error code `bad_request`

### Requirement: REL-17 Reply keyboard
`POST /v1/messages` SHALL accept an optional `reply_keyboard`: a list of rows, each row a list of button texts. It SHALL be stored with the message and returned to the user in history and in the event stream. The limits are `Relay:MaxKeyboardButtons` (start 100) and `Relay:MaxButtonTextLength` (start 64); a keyboard over them SHALL get HTTP 400 with error code `bad_keyboard`.

#### Scenario: Bot sends a keyboard
- **WHEN** the bot posts a message with `"reply_keyboard":[["Yes","No"],["Cancel"]]`
- **THEN** the response has the same keyboard and the user's history returns that message with the same keyboard

#### Scenario: Message without a keyboard
- **WHEN** the bot posts a message without `reply_keyboard`
- **THEN** the message in the user's history has no `reply_keyboard` field

#### Scenario: Remove the keyboard
- **WHEN** the bot posts a message with `"reply_keyboard":[]`
- **THEN** the message in the user's history has `reply_keyboard` as an empty list

#### Scenario: Keyboard over the limits
- **WHEN** the bot posts a keyboard with too many buttons, an empty button text or a too long button text
- **THEN** the response is 400 `bad_keyboard` and nothing is stored

### Requirement: REL-18 Client settings
`GET /api/settings` SHALL return the settings the client needs: `max_text_length` (`Relay:MaxTextLength`), `sync_page_size` (`Relay:MaxMessagesPerSync`), `reconnect_min_seconds` and `reconnect_max_seconds` (`Relay:ClientReconnectMinSeconds`, start 1, and `Relay:ClientReconnectMaxSeconds`, start 30). The client SHALL NOT hard-code these values.

#### Scenario: Read the settings
- **GIVEN** `Relay:MaxTextLength` is 10
- **WHEN** a logged-in user calls `GET /api/settings`
- **THEN** the response has `max_text_length` 10, the page size and both reconnect values
