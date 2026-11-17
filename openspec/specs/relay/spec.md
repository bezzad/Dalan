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
`GET /v1/updates` SHALL return `{"updates":[...]}`. Each update SHALL have `update_id` and either a `message` or a `callback` (REL-21). A `message` has `id` (the sequence number in the chat), `from` (`id`, `name`, `username` of the user), `text`, `date` (UTC) and, when it carries one, `file` (REL-28).

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
`GET /api/bots/{bot id}/messages` SHALL return `{"messages":[...],"pinned":...}` of this user's chat with that bot only, in rising `id` order (rising `version` with `after`). `pinned` is the chat's pinned message or null. `after` gives the messages with a higher `version`; `before` those with a lower `id`; `limit` sets the size, at most `Relay:MaxMessagesPerSync` (start 100).

#### Scenario: Fields of a message
- **WHEN** the history is read
- **THEN** each message has `id`, `version`, `from` ("user" or "bot"), `text`, `date` and, when set, `reply_keyboard`, `inline_keyboard`, `file`, `edited`, `pinned` and `deleted`

#### Scenario: Open a chat
- **GIVEN** a chat with messages 1 to 5
- **WHEN** the user calls it with `limit=3` and no other parameter
- **THEN** the newest messages 3, 4 and 5 are returned in that order

#### Scenario: Limit not given or too big
- **GIVEN** the maximum is 2 and the chat has 5 messages
- **WHEN** the user calls it with no `limit`, or with `limit=50`
- **THEN** 2 messages are returned

#### Scenario: Sync after a number
- **GIVEN** a chat with messages 1 to 5 and no change
- **WHEN** the user calls it with `after=2&limit=2`
- **THEN** the messages 3 and 4 are returned (the oldest after version 2)

#### Scenario: Sync brings a change
- **GIVEN** a chat with messages 1 to 3, and the bot then edits message 1
- **WHEN** the user calls it with `after=3`
- **THEN** only message 1 is returned, with its new text and `version` 4

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
`GET /api/settings` SHALL return the settings the client needs: `max_text_length` (`Relay:MaxTextLength`), `max_caption_length` (`Relay:MaxCaptionLength`), `max_file_size_mb` (`Relay:MaxFileSizeMb`), `sync_page_size` (`Relay:MaxMessagesPerSync`), `reconnect_min_seconds` and `reconnect_max_seconds` (`Relay:ClientReconnectMinSeconds`, start 1, and `Relay:ClientReconnectMaxSeconds`, start 30). The client SHALL NOT hard-code these values.

#### Scenario: Read the settings
- **GIVEN** `Relay:MaxTextLength` is 10
- **WHEN** a logged-in user calls `GET /api/settings`
- **THEN** the response has `max_text_length` 10, the caption and file limits, the page size and both reconnect values

### Requirement: REL-19 One contract for every bot door
Relay SHALL offer what a bot may do as the contract `IBotGateway` in `Dalan.Relay.Contracts`: enter with a token (REL-04, SEC-01), get updates, send a message or a file, edit, delete, pin and unpin, answer a button press, set and get commands, and get a file. The core Bot API and every adapter plugin SHALL use this contract, so both doors share one queue and one set of rules.

#### Scenario: Read on one door, confirm on the other
- **GIVEN** the bot got an update through the core Bot API
- **WHEN** it confirms it through the Telegram adapter with the next offset
- **THEN** the message is marked "delivered" and no door returns it again

#### Scenario: Send on one door, same chat
- **GIVEN** a user who started the bot
- **WHEN** the bot sends one message through the core Bot API and one through the Telegram adapter
- **THEN** the user's history has both, with sequence numbers that follow each other

### Requirement: REL-20 Inline keyboard
`POST /v1/messages` SHALL accept an optional `inline_keyboard`: rows of buttons, each with `text` and exactly one of `callback_data` (1 to `Relay:MaxCallbackDataBytes` UTF-8 bytes, start 64) or `url` (`http` or `https`, at most `Relay:MaxButtonUrlLength`, start 2048). Count and text follow REL-17. It SHALL be stored and returned in history and in the event stream. A keyboard that breaks a rule SHALL get 400 `bad_keyboard`, and nothing is stored.

#### Scenario: Bot sends inline buttons
- **WHEN** the bot posts a message with `"inline_keyboard":[[{"text":"Yes","callback_data":"y"},{"text":"Site","url":"https://dalanet.ir"}]]`
- **THEN** the user's history has the message with the same buttons

#### Scenario: Broken button
- **WHEN** a button has both `callback_data` and `url`, neither, data over the limit, or a `url` that is not http or https
- **THEN** the response is 400 `bad_keyboard` and nothing is stored

### Requirement: REL-21 A user presses an inline button
`POST /api/bots/{bot id}/callbacks` with `{"message_id":N,"data":"..."}` SHALL queue a `callback` update and return `{"id":"..."}` when message N of the chat is a bot message, not deleted, with a button of exactly that `callback_data`. Otherwise: 404 `button_not_found`, nothing queued. The update has `callback` with `id`, `from`, `message` and `data`. A press SHALL NOT be stored in history, and follows the queue rules (Q-02 to Q-06, Q-08) except restarts.

#### Scenario: Press reaches the bot
- **GIVEN** bot message 2 has a button with data "y"
- **WHEN** the user presses it
- **THEN** the bot's next update has `callback.data` "y", `callback.message.id` 2 and the user in `from`

#### Scenario: Invented data
- **WHEN** the user sends data that no button of that message has
- **THEN** the response is 404 `button_not_found` and the bot gets nothing

#### Scenario: Not in history
- **WHEN** the user presses a button
- **THEN** the chat history has no new message

### Requirement: REL-22 The bot answers a press
`POST /v1/callbacks/{id}/answer` with optional `text` (at most `Relay:MaxCallbackAnswerLength`, start 200) and `show_alert` SHALL send an `answer` event to the open streams of the user who pressed (LIVE-09), and return `true`. It SHALL work only for presses of this bot made at most `Relay:CallbackAnswerSeconds` ago (start 120); otherwise 400 `callback_not_found`.

#### Scenario: Answer with a text
- **GIVEN** the user pressed a button and has an open stream
- **WHEN** the bot answers with "Saved" and `show_alert` true
- **THEN** the stream gets an `answer` event with that text and `show_alert` true

#### Scenario: Too late or not this bot's
- **WHEN** a bot answers an unknown id, another bot's press, or a press older than the setting
- **THEN** the response is 400 `callback_not_found`

### Requirement: REL-23 Edit a message
`PATCH /v1/messages/{user id}/{message id}` with optional `text` and optional `inline_keyboard` SHALL change a bot message of that chat: the text when given, and the inline keyboard always (missing = removed). It gets the next version and `edited`, is returned like REL-08, and reaches the streams (LIVE-02). Errors: 404 `message_not_found` (none, or deleted), 400 `not_editable` (user message), `no_text` (file message), `not_modified` (nothing changed), and those of REL-09 and REL-20.

#### Scenario: New text
- **GIVEN** bot message 2 says "wait"
- **WHEN** the bot edits it to "done"
- **THEN** the history has message 2 with "done", a new `version` and `edited`

#### Scenario: Buttons removed
- **GIVEN** bot message 2 has inline buttons
- **WHEN** the bot edits only its text
- **THEN** message 2 has no inline buttons

#### Scenario: Not allowed
- **WHEN** the bot edits a user's message, a message that does not exist, or with the same text and buttons
- **THEN** the response is 400 `not_editable`, 404 `message_not_found`, or 400 `not_modified`

### Requirement: REL-24 Delete a message
`DELETE /v1/messages/{user id}/{message id}` SHALL delete any message of that chat that is not deleted yet, and return `true`. The message keeps its `id` with `deleted` true, an empty text, no buttons, no file and the next version, so a syncing client removes it. A deleted pinned message is no longer pinned. Otherwise 404 `message_not_found`.

#### Scenario: Delete
- **WHEN** the bot deletes message 2
- **THEN** a sync returns message 2 with `deleted` true and no text

#### Scenario: Twice
- **WHEN** the bot deletes message 2 again
- **THEN** the response is 404 `message_not_found`

### Requirement: REL-25 Pin a message
`PUT /v1/chats/{user id}/pin` with `{"message_id":N}` SHALL pin message N of that chat and unpin the one pinned before; `DELETE /v1/chats/{user id}/pin` SHALL unpin it. Each changed message gets the next version. A chat has at most one pinned message. Pinning a missing or deleted message is 404 `message_not_found`.

#### Scenario: Pin
- **WHEN** the bot pins message 2
- **THEN** the history answer has `pinned` with message 2

#### Scenario: Pin another
- **GIVEN** message 2 is pinned
- **WHEN** the bot pins message 3
- **THEN** `pinned` is message 3, and message 2 has no `pinned` mark

#### Scenario: Unpin
- **WHEN** the bot unpins
- **THEN** `pinned` is null

### Requirement: REL-26 Commands
`PUT /v1/commands` with `{"commands":[{"command":"start","description":"..."}]}` SHALL replace the bot's command list; `GET /v1/commands` SHALL return it. `GET /api/bots/{bot id}/commands` SHALL return it to a user. A command is 1 to 32 of `a-z`, `0-9` and `_`; a description has 1 to `Relay:MaxCommandDescriptionLength` characters (start 256); at most `Relay:MaxCommands` (start 100). A list that breaks a rule SHALL get 400 `bad_commands` and change nothing.

#### Scenario: Set and read
- **WHEN** the bot sets the commands "start" and "help"
- **THEN** the bot and the user both read the same two commands

#### Scenario: Bad command
- **WHEN** the bot sets "Start!" as a command
- **THEN** the response is 400 `bad_commands`

### Requirement: REL-27 A bot sends a file
`POST /v1/messages` as `multipart/form-data` with the part `file`, `user_id`, optional `kind` (start `document`), `text` (caption, at most `Relay:MaxCaptionLength`, start 1024) and keyboards as JSON SHALL send a message that carries the file. `file_id` instead of the part SHALL send again a file this bot has. The message has `file` with `id`, `kind`, `name`, `type` and `size`. Over `Relay:MaxFileSizeMb` (start 20): 413 `file_too_big`; unknown `file_id`: 400 `file_not_found`.

#### Scenario: Send a photo
- **WHEN** the bot uploads `cat.jpg` as kind `photo` with caption "cat"
- **THEN** the user's history has a message with text "cat" and a `file` of kind `photo`, name `cat.jpg` and type `image/jpeg`

#### Scenario: Send again by id
- **GIVEN** the bot sent a file to user A
- **WHEN** it sends the same `file_id` to user B
- **THEN** user B's message has the same file and user B can download it

#### Scenario: Too big
- **WHEN** the bot uploads a file one byte over the limit
- **THEN** the response is 413 `file_too_big` and nothing is stored

### Requirement: REL-28 A user sends a file
`POST /api/bots/{bot id}/files` with the file as the body, its type as `Content-Type`, the header `X-File-Name` and optional `X-Caption` (both percent-encoded) SHALL send a user message that carries the file, like REL-15. Kind: `image/jpeg|png|gif|webp` photo, `audio/*` audio, `video/*` video, else document. The bot gets it with its `file` (REL-07). Limits: `Relay:MaxFileSizeMb` (413 `file_too_big`) and `Relay:UserUploadMbPerDay` (start 200) in 24 hours (429 `upload_limit`).

#### Scenario: Send a photo
- **WHEN** the user uploads a PNG named "شکل.png"
- **THEN** the bot's update has a message with a `file` of kind `photo` and name "شکل.png"

#### Scenario: No name
- **WHEN** the upload has no `X-File-Name`
- **THEN** the response is 400 `bad_request` and nothing is stored

#### Scenario: Daily limit
- **GIVEN** the user uploaded files up to the daily limit
- **WHEN** the user uploads one more
- **THEN** the response is 429 `upload_limit`

### Requirement: REL-29 Get a file
`GET /v1/files/{file id}` SHALL return the bytes of a file of this bot, with `Content-Length`. `GET /api/bots/{bot id}/files/{file id}` SHALL return a file to a user only when a message of that user's chat with that bot, not deleted, carries it. An unknown or expired file, or one the caller may not see, SHALL get 404 `file_not_found`.

#### Scenario: Bot downloads a user's file
- **GIVEN** a user sent a file to the bot
- **WHEN** the bot gets that file id
- **THEN** the body is the same bytes

#### Scenario: Another user's file
- **GIVEN** user A sent a file to a bot
- **WHEN** user B asks for that file id through the same bot
- **THEN** the response is 404 `file_not_found`

### Requirement: REL-30 Files expire
A file SHALL expire `Relay:FileRetentionDays` (start 30) after it was saved. The file of a user message that ends "not delivered" SHALL expire at once. A sweep every `Relay:FileSweepMinutes` (start 60) SHALL delete expired files from the disk and the table.

#### Scenario: Old file
- **GIVEN** a file saved more than the retention time ago
- **WHEN** the sweep runs
- **THEN** the file is gone from disk and a download answers 404 `file_not_found`

#### Scenario: Bot never read it
- **GIVEN** a user's file message that expired before the bot read it
- **WHEN** the sweep runs
- **THEN** the file is gone from disk
