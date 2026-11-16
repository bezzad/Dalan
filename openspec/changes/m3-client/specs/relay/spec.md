## MODIFIED Requirements

### Requirement: REL-11 Errors explain themselves
Every Bot API and Client API error SHALL be JSON with `error` (a stable code) and `message` (what went wrong and how to fix it).

#### Scenario: Any error
- **WHEN** a Bot API call fails with 400, 401, 403, 409 or 429
- **THEN** the body has a non-empty `error` and a non-empty `message`

#### Scenario: Any Client API error
- **WHEN** a Client API call fails with 400, 401, 404 or 429
- **THEN** the body has a non-empty `error` and a non-empty `message`

## ADDED Requirements

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
`GET /api/settings` SHALL return the settings the client needs: `max_text_length` (`Relay:MaxTextLength`), `reconnect_min_seconds` and `reconnect_max_seconds` (`Relay:ClientReconnectMinSeconds`, start 1, and `Relay:ClientReconnectMaxSeconds`, start 30). The client SHALL NOT hard-code these values.

#### Scenario: Read the settings
- **GIVEN** `Relay:MaxTextLength` is 10
- **WHEN** a logged-in user calls `GET /api/settings`
- **THEN** the response has `max_text_length` 10 and both reconnect values
