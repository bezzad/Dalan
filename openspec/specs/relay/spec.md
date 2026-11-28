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
`GET /api/bots` SHALL return `{"bots":[...]}` with every bot this user has a chat with, newest chat first, and no other bot. Each bot has `id`, `username`, `name`, `unread` (REL-32), `online` and `last_seen` (REL-33).

#### Scenario: Two users, two bots
- **GIVEN** user A wrote to bot 1 and user B wrote to bot 2
- **WHEN** user A calls `GET /api/bots`
- **THEN** the list holds bot 1 only

#### Scenario: No chat yet
- **WHEN** a new user calls `GET /api/bots`
- **THEN** the list is empty

#### Scenario: Unread and status in the list
- **GIVEN** a user wrote to a bot that never polled, and the bot then sent two messages
- **WHEN** the user calls `GET /api/bots`
- **THEN** that bot has `unread` 2, `online` false and `last_seen` null

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
- **THEN** each message has `id`, `version`, `from` ("user" or "bot"), `text`, `date` and, when set, `reply_keyboard`, `inline_keyboard`, `file`, `edited`, `pinned`, `deleted` and `not_delivered`

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
`POST /v1/messages` SHALL accept an optional `reply_keyboard` (rows of buttons: a text or `{"text", "style"}`) and flags `reply_keyboard_persistent`, `_resize`, `_one_time` (default false). It SHALL be stored and returned in history and the stream: `reply_keyboard` as rows of texts, plus `reply_keyboard_options` (flags and `styles`) when a flag or style is set. Rows keep order and size. Over `Relay:MaxKeyboardButtons` (100) or `Relay:MaxButtonTextLength` (64), or a bad style: 400 `bad_keyboard`.

#### Scenario: Bot sends a keyboard
- **WHEN** the bot posts a message with `"reply_keyboard":[["Yes","No"],["Cancel"]]`
- **THEN** the response has the same keyboard and the user's history returns that message with the same keyboard and no `reply_keyboard_options`

#### Scenario: Rows, style and flags kept
- **WHEN** the bot posts rows of 5, 4 and 3 buttons, the last one `{"text":"➕","style":"success"}`, with `reply_keyboard_resize` true
- **THEN** the user's history has three rows of 5, 4 and 3 texts, and `reply_keyboard_options` has `resize` true, `persistent` false, `one_time` false and `success` at row 3, button 3, null elsewhere

#### Scenario: Message without a keyboard
- **WHEN** the bot posts a message without `reply_keyboard`
- **THEN** the message in the user's history has no `reply_keyboard` field

#### Scenario: Remove the keyboard
- **WHEN** the bot posts a message with `"reply_keyboard":[]`
- **THEN** the message in the user's history has `reply_keyboard` as an empty list

#### Scenario: Keyboard over the limits
- **WHEN** the bot posts a keyboard with too many buttons, an empty button text, a too long button text, or the style `pink`
- **THEN** the response is 400 `bad_keyboard` and nothing is stored

### Requirement: REL-18 Client settings
`GET /api/settings` SHALL return what the client needs, each from the `Relay` setting of the same name: `max_text_length`, `max_caption_length`, `max_file_size_mb`, `sync_page_size` (`MaxMessagesPerSync`), `reconnect_min_seconds` and `reconnect_max_seconds` (`ClientReconnect...`, 1 and 30), `status_refresh_seconds` (`ClientStatusRefreshSeconds`, 30), `max_bot_icon_kb` and `press_wait_seconds` (`ClientPressWaitSeconds`, 15). The client SHALL NOT hard-code them.

#### Scenario: Read the settings
- **GIVEN** `Relay:MaxTextLength` is 10 and `Relay:ClientPressWaitSeconds` is 9
- **WHEN** a logged-in user calls `GET /api/settings`
- **THEN** the response has `max_text_length` 10, `press_wait_seconds` 9, the caption and file limits, the page size, both reconnect values and `status_refresh_seconds`

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
`POST /v1/messages` SHALL accept an optional `inline_keyboard`: rows of buttons with `text`, an optional `style` and one of `callback_data` (1 to `Relay:MaxCallbackDataBytes` bytes, 64), `url` (http or https, up to `Relay:MaxButtonUrlLength`, 2048) or `switch_query` (up to `Relay:MaxTextLength`; a tap puts `@username` and it in the input). Count and text follow REL-17. It SHALL be stored and returned with `style` in history and the stream. A broken one: 400 `bad_keyboard`, nothing stored.

#### Scenario: Bot sends inline buttons
- **WHEN** the bot posts a message with `"inline_keyboard":[[{"text":"Yes","callback_data":"y","style":"danger"},{"text":"Site","url":"https://dalanet.ir"}]]`
- **THEN** the user's history has the message with the same buttons, the first with `style` `danger`, the second with no `style`

#### Scenario: Fill button
- **WHEN** the bot posts a button `{"text":"Edit","switch_query":"todo 7 "}`
- **THEN** the user's history has the button with `switch_query` "todo 7 "

#### Scenario: Broken button
- **WHEN** a button has two of `callback_data`, `url` and `switch_query`, none, data over the limit, a `url` that is not http or https, or an unknown style
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
`PATCH /v1/messages/{user id}/{message id}` with optional `text`, `entities` (REL-53) and `inline_keyboard` SHALL edit a bot message of that chat: the text (a file's caption) and entities when given, else kept; the keyboard always (missing = removed). It gets the next version and `edited`, is returned like REL-08 and reaches the streams (LIVE-02). Errors: 404 `message_not_found`, 400 `not_editable`, `not_modified`, and those of REL-09, REL-20, REL-53.

#### Scenario: New text
- **GIVEN** bot message 2 says "wait"
- **WHEN** the bot edits it to "done"
- **THEN** the history has message 2 with "done", a new `version` and `edited`

#### Scenario: New caption
- **GIVEN** bot message 2 is a photo with caption "cat"
- **WHEN** the bot edits it with text "dog"
- **THEN** message 2 still has the photo and its caption is "dog"

#### Scenario: Buttons removed
- **GIVEN** bot message 2 has inline buttons
- **WHEN** the bot edits only its text
- **THEN** message 2 has no inline buttons

#### Scenario: Not allowed
- **WHEN** the bot edits a user's message, a message that does not exist, or with the same text, entities and buttons
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
`POST /api/bots/{bot id}/files` (body the file, `Content-Type`, `X-File-Name`, optional `X-Caption`, percent-encoded) SHALL send a user message with it, like REL-15. `X-Voice-Duration` (seconds) with `audio/*` is voice (bad: 400 `bad_request`); else `image/jpeg|png|gif|webp` photo, `audio/*` audio, `video/*` video, else document. The bot gets its `file` (REL-07). Limits: `Relay:MaxFileSizeMb` (413 `file_too_big`), `Relay:UserUploadMbPerDay` (200) a day (429 `upload_limit`).

#### Scenario: Send a photo
- **WHEN** the user uploads a PNG named "شکل.png"
- **THEN** the bot's update has a message with a `file` of kind `photo` and name "شکل.png"

#### Scenario: Send a voice
- **WHEN** the user uploads `audio/ogg` named "voice.ogg" with `X-Voice-Duration: 7`
- **THEN** the bot's update has a `file` of kind `voice`, type `audio/ogg` and duration 7, and the user's history shows the same

#### Scenario: Audio without a duration
- **WHEN** the user uploads `audio/mpeg` with no `X-Voice-Duration`
- **THEN** the file is of kind `audio`

#### Scenario: No name
- **WHEN** the upload has no `X-File-Name`, or `X-Voice-Duration` is `abc` or `-1`
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

### Requirement: REL-31 "Not delivered" is shown
When a user message becomes "not delivered" (Q-02, Q-04, Q-07), it SHALL get the next `version` of its chat and a `MessageChanged` event, in the same transaction. The message then has `not_delivered` true. A "delivered" message gets no new version.

#### Scenario: Expired while the bot is away
- **GIVEN** a user message with `version` 1 that the bot never read
- **WHEN** its expiry passes and the sweep runs
- **THEN** the message has `not_delivered` true and `version` 2, and a sync with `after=1` returns it

#### Scenario: Replaced by a newer command
- **WHEN** a user sends two messages before the bot reads
- **THEN** the first has `not_delivered` true and the second has no `not_delivered`

#### Scenario: Delivered stays quiet
- **WHEN** the bot reads and confirms a user message
- **THEN** the message keeps its `version` and has no `not_delivered`

### Requirement: REL-32 Read position and unread count
Each chat SHALL keep the user's read position. `POST /api/bots/{bot id}/read` with JSON `{"message_id": N}` SHALL move it to N, but never back and never past the newest message, and SHALL answer `{"unread": n}`. Unread is the number of bot messages of the chat, not deleted, with an `id` above the read position. A message the user sends SHALL move the read position to that message.

#### Scenario: Read up to a message
- **GIVEN** a chat with user message 1 and bot messages 2, 3 and 4
- **WHEN** the user reads up to 3
- **THEN** the answer has `unread` 1

#### Scenario: Never back
- **GIVEN** the user read up to 4
- **WHEN** the user reads up to 2
- **THEN** the read position stays 4 and `unread` is 0

#### Scenario: Sending reads the chat
- **GIVEN** a chat with two unread bot messages
- **WHEN** the user sends a message
- **THEN** that bot has `unread` 0 in the list

#### Scenario: Bad body or unknown chat
- **WHEN** the user reads with no `message_id`, or in a chat they do not have
- **THEN** the response is 400 with `bad_request`, or 404 with `bot_not_found`

### Requirement: REL-33 Bot status
The server SHALL remember when each bot last called get-updates, through any bot door. A bot is online while a get-updates call of it runs, and for `Relay:BotOnlineSeconds` (start 60) after its last call ended. `GET /api/bots/{bot id}/status` SHALL answer `{"online": bool, "last_seen": time or null}`. The last-seen time SHALL survive a restart (it may be up to `BotOnlineSeconds` older there).

#### Scenario: Polling now
- **WHEN** a bot's get-updates call is waiting
- **THEN** the status is `online` true

#### Scenario: Gone quiet
- **GIVEN** a bot's last get-updates call ended at 12:00
- **WHEN** a user asks at 12:02 with the setting at 60
- **THEN** the status is `online` false and `last_seen` 12:00

#### Scenario: Never polled
- **WHEN** a user asks for a bot that never called get-updates
- **THEN** the status is `online` false and `last_seen` null

#### Scenario: After a restart
- **GIVEN** a bot polled and the server restarted
- **WHEN** a user asks
- **THEN** `last_seen` is not null

### Requirement: REL-34 New bot messages are pushed
Every new message from a bot SHALL be passed to Notification as a push for its user (NTF-06), after it is saved, through the outbox (LIVE-04). Its title is the bot's name, its body the text or else the file name, its link `/@<bot username>` and its tag `bot-<bot id>`. Edits, deletes, pins and the user's own messages SHALL NOT be pushed. A push SHALL NOT slow the bot's send.

#### Scenario: Bot writes
- **WHEN** bot "Shop" (`@shopbot`) sends "Your order is ready" to a user
- **THEN** one push for that user is passed on with title "Shop", body "Your order is ready", link `/@shopbot`

#### Scenario: A file without caption
- **WHEN** the bot sends the file `menu.pdf` with no caption
- **THEN** the push body is `menu.pdf`

#### Scenario: Nothing else is pushed
- **WHEN** the user sends a message and the bot edits and pins its own message
- **THEN** no push is passed on

### Requirement: REL-35 Push subscriptions door
The Client API SHALL have `GET /api/push` → `{"key": the VAPID public key, or null when push is off}`, `POST /api/push/subscribe` with the browser's subscription JSON (`endpoint`, `keys.p256dh`, `keys.auth`), and `POST /api/push/unsubscribe` with `{"endpoint"}`. Subscribe answers 204, or 400 `bad_subscription` (NTF-04), or 503 `push_off`. Unsubscribe answers 204 and removes the endpoint only if it belongs to this user. These are Client API calls (REL-12, ADM-01, SEC-03).

#### Scenario: Turn on
- **GIVEN** push keys are set
- **WHEN** a user subscribes with a valid subscription
- **THEN** the answer is 204 and the subscription is stored for that user

#### Scenario: Push off
- **GIVEN** no push keys are set
- **WHEN** a user asks `GET /api/push` and then subscribes
- **THEN** `key` is null, and the subscribe answer is 503 with `push_off`

#### Scenario: Turn off
- **GIVEN** a user's stored subscription
- **WHEN** another user unsubscribes its endpoint, and then the owner does
- **THEN** it stays after the first call and is gone after the second

### Requirement: REL-36 A builder creates bots
`POST /api/builder/bots` with `{"username", "name"}` SHALL create a bot owned by the caller and answer 201 with `id`, `username`, `name` and the `token`, the only answer that holds it. Username: 5 to 32 of `a-z 0-9 _`, starting with a letter, made lower case, unique (409 `username_taken`); name: 1 to `Relay:MaxBotNameLength` (start 64); else 400 `bad_request`. At most `Relay:MaxBotsPerBuilder` (start 20) per owner (409 `bot_limit`). A non-builder (AUTH-19) gets 403 `not_builder`.

#### Scenario: Create
- **GIVEN** an approved builder
- **WHEN** the builder creates `@Weather_Bot` named "Weather"
- **THEN** the answer is 201 with username `weather_bot` and a token that works on the Bot API

#### Scenario: Not a builder
- **WHEN** a user who is not a builder creates a bot
- **THEN** the answer is 403 `not_builder` and no bot exists

#### Scenario: Taken
- **GIVEN** a bot `weather_bot` exists
- **WHEN** a builder creates `weather_bot`
- **THEN** the answer is 409 `username_taken`

#### Scenario: Too many
- **GIVEN** `Relay:MaxBotsPerBuilder` is 1 and the builder has one bot
- **WHEN** the builder creates another
- **THEN** the answer is 409 `bot_limit`

### Requirement: REL-39 My bots
`GET /api/builder/bots` SHALL list the caller's own bots (`id`, `username`, `name`, creation time, last seen time, `suspended`), never a token. The builder calls are Client API calls (REL-12, ADM-01, SEC-03).

#### Scenario: Only my bots
- **GIVEN** builder A has one bot and builder B another
- **WHEN** A lists the builder bots
- **THEN** only A's bot is in the answer, with no token

### Requirement: REL-37 Token rotation
`POST /api/builder/bots/{id}/token` SHALL give the bot a new token, answer it once, and make the old token stop at once. Only the owner (or an admin) SHALL do this; for any other bot the answer SHALL be 404 `bot_not_found`. The rotation is in the audit log (ADM-09) without the token.

#### Scenario: Old token stops
- **GIVEN** a builder's bot with token T1
- **WHEN** the builder rotates the token and gets T2
- **THEN** a Bot API call with T1 gets 401 and one with T2 works

#### Scenario: Another owner's bot
- **WHEN** builder B rotates the token of builder A's bot
- **THEN** the answer is 404 `bot_not_found` and A's token still works

### Requirement: REL-48 Rename a bot
`PATCH /api/builder/bots/{id}` with `{"name":"..."}` SHALL change the bot's display name (1 to `Relay:MaxBotNameLength` characters, trimmed) and answer the bot like the list. The username SHALL NOT change. Only the owner (or an admin); for any other bot 404 `bot_not_found`. A bad name gets 400 `bad_request`. The change is in the audit log (ADM-09).

#### Scenario: New name, same username
- **WHEN** the owner renames bot `weather_bot` to "Arcane"
- **THEN** the answer has name "Arcane" and username `weather_bot`

### Requirement: REL-49 Delete a bot
`DELETE /api/builder/bots/{id}` SHALL delete the bot at once for its owner and its users: its token stops and it leaves every list and the showcase. Its chats SHALL stay for the admin until the normal history expiry (owner, 2026-11-20). The username SHALL be free again; chats belong to the bot id, never the username. Only the owner (or an admin); for any other bot 404 `bot_not_found`. The delete is in the audit log (ADM-09).

#### Scenario: Gone, chats kept, and the name is free
- **GIVEN** a builder's bot `weather_bot` with a chat
- **WHEN** the builder deletes it
- **THEN** its token gets 401, it is in no list, its messages stay in the database, and a new bot may take `weather_bot` with a new id and no old chats

### Requirement: REL-38 Suspended bot
A bot suspended by an admin SHALL get 403 `bot_suspended` on every core Bot API call with its valid token; the access log row SHALL still be written (ADM-01). A user who sends a message or a file to it, presses one of its buttons or starts it SHALL get 403 `bot_suspended`. `GET /api/bots/{id}` and the bot list SHALL say `suspended` true. After unsuspend the bot SHALL work again with the same token.

#### Scenario: Bot call
- **GIVEN** bot 5 is suspended
- **WHEN** bot 5 calls `GET /v1/updates` from `203.0.113.7`
- **THEN** the answer is 403 `bot_suspended` and the access log has the row

#### Scenario: User writes
- **GIVEN** a suspended bot
- **WHEN** a user sends it a text
- **THEN** the answer is 403 `bot_suspended` and nothing is stored

#### Scenario: Unsuspend
- **GIVEN** a suspended bot
- **WHEN** an admin unsuspends it
- **THEN** its next `GET /v1/updates` works

### Requirement: REL-40 A builder asks to list a bot
`PUT /api/builder/bots/{id}/showcase` with `{"description", "category"}` SHALL create or change the bot's showcase entry and set it to `pending`. Description: 1 to `Relay:MaxBotDescriptionLength` (start 512) characters; category: one of `Relay:ShowcaseCategories`; else 400 `bad_request`. Another owner's bot SHALL get 404 `bot_not_found`. `GET /api/builder/bots` SHALL give each bot's `showcase` or null.

#### Scenario: Ask
- **GIVEN** a builder's bot that is not in the showcase
- **WHEN** the builder sends a description and the category `games`
- **THEN** the answer is 200 with state `pending`, the builder's list shows `state`, `description`, `category` and `icon`, and the audit log has `showcase.request`

#### Scenario: A change goes back to review
- **GIVEN** a listed entry
- **WHEN** the builder changes its description
- **THEN** the state is `pending` and the bot is no longer in the public list

#### Scenario: Unknown category
- **WHEN** the builder sends the category `casino`
- **THEN** the answer is 400 `bad_request`

#### Scenario: Another owner's bot
- **WHEN** builder B asks to list builder A's bot
- **THEN** the answer is 404 `bot_not_found` and no entry exists

### Requirement: REL-41 Showcase icon
The showcase SHALL show the bot's icon (REL-51); it has no icon of its own. `PUT /api/builder/bots/{id}/showcase/icon` SHALL stay as another address of the bot icon `PUT` (REL-51), with its checks, and SHALL NOT change the entry's state; `GET` there SHALL give the bot icon to the owner or an admin. Showcase answers SHALL say `icon` true when the bot has one.

#### Scenario: PNG icon
- **GIVEN** a bot with a showcase entry
- **WHEN** the builder sends a PNG image to the showcase icon address
- **THEN** the bot has the icon, the entry keeps its state, and `GET` gives the same bytes as `image/png`

#### Scenario: Not an image
- **WHEN** the builder sends an SVG file or a text
- **THEN** the answer is 400 `bad_icon`

#### Scenario: Too big
- **GIVEN** `Relay:MaxBotIconKb` is 1
- **WHEN** the builder sends a 2 KB PNG
- **THEN** the answer is 400 `bad_icon`

#### Scenario: Icon through the old address
- **GIVEN** a listed entry
- **WHEN** the builder sends a PNG to the showcase icon address
- **THEN** the bot has that icon, the entry is still `listed`, and `GET /api/showcase/{id}/icon` gives the same bytes

#### Scenario: Old icons kept
- **GIVEN** a showcase entry with an icon before this change
- **WHEN** the server starts with this change
- **THEN** the bot has that icon

### Requirement: REL-42 Leave the showcase
`DELETE /api/builder/bots/{id}/showcase` SHALL delete the entry at once, with no review. It is in the audit log (`showcase.leave`).

#### Scenario: Leave
- **GIVEN** a listed bot
- **WHEN** its builder deletes the entry
- **THEN** the bot is not in the public list and its icon gets 404

### Requirement: REL-43 Public showcase
`GET /api/showcase` SHALL need no session and give `categories` and `bots`: only bots whose entry is `listed` and that are not suspended, in rising id order, at most `Relay:ShowcasePageSize` (start 50). `category=<key>` keeps one category; `after=<id>` gives the next page. `GET /api/showcase/{id}/icon` SHALL need no session and give the icon of a listed, not suspended bot, else 404 `not_found`.

#### Scenario: Only listed bots
- **GIVEN** bot A is listed, bot B is pending, bot C was refused and bot D has no entry
- **WHEN** a visitor with no session asks for the showcase
- **THEN** only bot A is in the answer, with `id`, `username`, `name`, `description`, `category` and `icon` (true or false)

#### Scenario: Suspended bot
- **GIVEN** a listed bot that the admin suspends
- **WHEN** a visitor asks for the showcase and the bot's icon
- **THEN** the bot is not in the list and the icon gets 404

#### Scenario: One category, next page
- **GIVEN** `Relay:ShowcasePageSize` is 1 and two listed games and one listed tool
- **WHEN** a visitor asks with `category=games`, then with `after` the first answer's id
- **THEN** each answer has one game, and no tool

### Requirement: REL-44 History kept per chat
A sweeper SHALL delete, every `Relay:HistorySweepMinutes` (start 60), the messages of each chat that are older than `Relay:HistoryDays` (start 30) days or are not among its newest `Relay:HistoryMessages` (start 200). A user message that still waits for the bot SHALL NOT be deleted. Sequence numbers and versions SHALL keep counting from where they were.

#### Scenario: Only the newest stay
- **GIVEN** `Relay:HistoryMessages` is 3 and a chat with messages 1 to 5
- **WHEN** the sweeper runs
- **THEN** the history has messages 3, 4 and 5, and the next message gets sequence number 6

#### Scenario: Old messages go
- **GIVEN** `Relay:HistoryDays` is 30 and a chat with a message 31 days old and one from today
- **WHEN** the sweeper runs
- **THEN** only the message from today is left

#### Scenario: A waiting message stays
- **GIVEN** `Relay:HistoryMessages` is 1 and a chat whose older message is a user message the bot has not read
- **WHEN** the sweeper runs
- **THEN** that message is still there

### Requirement: REL-45 Bot status for its builder
`GET /api/builder/bots/{id}` SHALL give the owner, or an admin, one bot: `id`, `username`, `name`, `created_at`, `online`, `last_seen`, `suspended` and `showcase`, with `online` by the same rule as PRE-01. The list (`GET /api/builder/bots`) SHALL give `online` for each bot too. Another user's bot or an unknown id SHALL get 404 `bot_not_found`.

#### Scenario: Online bot
- **GIVEN** a builder's bot that polled a moment ago
- **WHEN** the builder asks for its status
- **THEN** `online` is true and `last_seen` is set

#### Scenario: Never polled
- **WHEN** the builder asks for a new bot's status
- **THEN** `online` is false and `last_seen` is null

#### Scenario: Another owner's bot
- **WHEN** builder B asks for builder A's bot
- **THEN** the answer is 404 `bot_not_found`

### Requirement: REL-46 MCP door
`/api/mcp` SHALL be a stateless Streamable HTTP MCP server (protocol 2026-07-28 and 2025-11-25) with the tools `list_bots`, `create_bot` (`username`, `name`), `rotate_token` (`bot_id`), `bot_status` (`bot_id`) and `set_bot_icon` (`bot_id`, `image_base64`). Each tool SHALL follow the rules and give the JSON of its REST call (REL-36, REL-37, REL-39, REL-45, REL-51). A refused call SHALL be a tool error with the same message.

#### Scenario: Tools
- **WHEN** an MCP client with a builder's key lists the tools
- **THEN** it gets `list_bots`, `create_bot`, `rotate_token`, `bot_status` and `set_bot_icon`

#### Scenario: Create and use
- **WHEN** the client calls `create_bot` with `weather_bot` and `Weather`
- **THEN** the result has the bot and its token, the token works at `GET /v1/updates`, and the audit log has `bot.create`

#### Scenario: Refused call
- **GIVEN** a bot named `weather_bot`
- **WHEN** the client calls `create_bot` with `weather_bot` again
- **THEN** the result is a tool error that says another bot has this username

#### Scenario: Icon by MCP
- **WHEN** the builder calls `set_bot_icon` with a PNG in base64
- **THEN** the bot has that icon; a text that is not base64 or not an image gives a tool error

### Requirement: REL-47 Who may call the MCP door
The MCP door SHALL take an access key or a session (AUTH-22), SHALL refuse a user who is not a builder with 403 `not_builder`, and SHALL write the access log (ADM-01) and count the per-user limit (SEC-03) on every call.

#### Scenario: No key
- **WHEN** an MCP client calls with no key and no session
- **THEN** the answer is 401

#### Scenario: Not a builder
- **WHEN** an MCP client calls with the key of a user who is not a builder
- **THEN** the answer is 403 `not_builder`

#### Scenario: Access log
- **WHEN** a builder calls the MCP door with a key
- **THEN** `admin.access_log` has a row for the user with the caller's address and port

### Requirement: REL-55 Chats follow the merge
On `UserMerged` (AUTH-34) every chat, file and push subscription of the gone user SHALL move to the kept user. A chat only the gone user has SHALL keep its message ids. Two chats with the same bot SHALL become one chat, its messages in time order with new ids after the largest old id of both, so an old id never points to another message. The kept user's clients SHALL load the chat again. No bot SHALL change owner.

#### Scenario: Only the gone user had the chat
- **GIVEN** user 9 has a chat with bot 5 with messages 1 to 3, and user 4 has none
- **WHEN** user 9 is merged into user 4
- **THEN** user 4 has the chat with bot 5 with messages 1 to 3

#### Scenario: Both had a chat
- **GIVEN** user 4 has messages 1 to 3 and user 9 has messages 1 to 2 with bot 5, sent at different times
- **WHEN** user 9 is merged into user 4
- **THEN** user 4 has one chat with 5 messages in time order, with ids 4 to 8, ties going to user 4 first and then by old id

#### Scenario: Read position
- **GIVEN** user 4 had read up to its message 3, which gets id 7
- **WHEN** the chats are joined
- **THEN** user 4's read position is 7

#### Scenario: Push subscriptions
- **GIVEN** the two users have more push subscriptions together than `MaxSubscriptionsPerUser`
- **WHEN** user 9 is merged into user 4
- **THEN** user 4 keeps the newest `MaxSubscriptionsPerUser` of them

### Requirement: REL-56 The bot is told the chat moved
For each bot that had a chat with the gone user, the bot SHALL get one update with `chat_moved`: `from_user_id` (the gone user), `to_user_id` (the kept user) and `date`. For `Relay:MovedChatDays` (start 180) after the merge, a call for the old user (send, edit, delete, pin) SHALL get 400 `chat_moved` with `to_user_id`; afterwards it SHALL get `chat_not_active`.

#### Scenario: The update
- **GIVEN** user 9 had a chat with bot 5
- **WHEN** user 9 is merged into user 4
- **THEN** bot 5 gets an update with `chat_moved`, `from_user_id` 9 and `to_user_id` 4

#### Scenario: Send to the old chat
- **WHEN** bot 5 sends a message to user 9 after the merge
- **THEN** the answer is 400 `chat_moved` with `to_user_id` 4, and after `Relay:MovedChatDays` it is 403 `chat_not_active`

### Requirement: REL-50 File length
A voice, audio or video file SHALL keep a length in whole seconds when one is given: by the user's upload (REL-28) or by the bot (`duration` on `POST /v1/messages` with a file, TG-20). History, the event stream and the bot's update SHALL carry it as `duration`; a file with no length has no `duration` field.

#### Scenario: Bot sends a voice with its length
- **WHEN** the bot sends a voice file with `duration` 12
- **THEN** the user's history has the file with `duration` 12

#### Scenario: Document
- **WHEN** a document is sent
- **THEN** its `file` has no `duration`

### Requirement: REL-51 Bot icon
A bot SHALL have at most one icon. `PUT /api/builder/bots/{id}/icon` (raw body) SHALL set it: PNG, JPEG or WebP by the first bytes, at most `Relay:MaxBotIconKb` (start 100) KB, else 400 `bad_icon`; `DELETE` removes it. Only the owner or an admin (else 404 `bot_not_found`); audit `bot.icon`. `GET /api/bots/{id}/icon` SHALL need no session; a deleted or suspended bot gets 404 `not_found`. Bot JSON SHALL have `icon_version`: 0 with no icon, higher after each change.

#### Scenario: Set and read
- **WHEN** the owner sends a PNG icon
- **THEN** the answer is 200, `GET /api/bots/{id}/icon` gives the same bytes as `image/png` with no session, and `icon_version` grew in the chat list of a user of that bot

#### Scenario: Not an image
- **WHEN** the owner sends an SVG, or a PNG over the limit
- **THEN** the answer is 400 `bad_icon` and the old icon stays

#### Scenario: Remove
- **WHEN** the owner deletes the icon
- **THEN** `GET /api/bots/{id}/icon` gives 404 and `icon_version` is 0

#### Scenario: Another owner's bot
- **WHEN** builder B sends an icon for builder A's bot
- **THEN** the answer is 404 `bot_not_found` and nothing changes

#### Scenario: Suspended bot
- **GIVEN** the bot has an icon and is suspended
- **WHEN** anyone asks for its icon
- **THEN** the answer is 404 `not_found`

### Requirement: REL-52 Reply to a message
A message MAY reply to a message of its chat: the user sends `reply_to` (a message id) in the JSON of REL-15 or the header `X-Reply-To` with a file (REL-28); a bot sends `reply_to` in `POST /v1/messages`. A target that is missing or deleted SHALL get 404 `message_not_found`, and nothing is stored. The message SHALL keep a copy of the target as it is then (`reply_to`: `id`, `from`, `text`, `file`) in history, events, updates and answers.

#### Scenario: User replies to the bot
- **GIVEN** bot message 2 says "Which session?"
- **WHEN** the user sends "the second" with `reply_to` 2
- **THEN** the bot's update has the message with `reply_to` `{"id":2,"from":"bot","text":"Which session?"}`, and the user's history has the same

#### Scenario: Bot replies to the user
- **GIVEN** user message 3
- **WHEN** the bot posts a message with `reply_to` 3
- **THEN** the user's history has the bot message with `reply_to.id` 3 and `reply_to.from` "user"

#### Scenario: The copy stays
- **GIVEN** bot message 4 replies to user message 3 "hello"
- **WHEN** the bot edits message 4
- **THEN** message 4 still has `reply_to.text` "hello"

#### Scenario: No such message
- **WHEN** the user or the bot replies to a message that does not exist, is deleted, or is of another chat
- **THEN** the response is 404 `message_not_found` and nothing is stored

### Requirement: REL-53 Text formatting
`POST /v1/messages` and REL-23 SHALL accept `entities` (Telegram's `{type, offset, length, url, language}`, in UTF-16 units) of types `bold`, `italic`, `underline`, `strikethrough`, `spoiler`, `code`, `pre`, `text_link`, `blockquote`, `expandable_blockquote`. Each SHALL lie inside the text; two SHALL be apart or nested; a `text_link` needs an http(s) `url`; at most `Relay:MaxTextEntities` (start 100). Else 400 `bad_entities`, nothing stored. They SHALL be kept and given out with the message.

#### Scenario: Bold word
- **WHEN** the bot posts "hi Ali" with `entities` `[{"type":"bold","offset":3,"length":3}]`
- **THEN** the user's history has the message with that entity

#### Scenario: Broken entities
- **WHEN** an entity goes past the end of the text, has an unknown type, a `text_link` has no http(s) `url`, or two entities cross
- **THEN** the response is 400 `bad_entities` and nothing is stored

### Requirement: REL-54 Install one bot
`GET /@{username}/manifest.webmanifest` SHALL need no session and give the bot's manifest: `id`, `start_url`, `scope` `/@username`, `name` and `short_name` its name, `display` "standalone"; icons only the bot's (REL-51), real size read when saved, purpose "any", when square and 192 or more, else Dalan's. Chrome takes only PNG, SVG or WebP, so a JPEG icon SHALL be listed as `/api/bots/{id}/icon.svg`: an SVG of the same size that holds the JPEG as it is, `image/svg+xml`, with a policy that loads nothing else. `GET /@{username}/icon` SHALL give that icon, else Dalan's 192. Unknown, deleted or suspended: 404. The `/@{username}` page SHALL link these instead of the main ones.

#### Scenario: Manifest of a bot
- **GIVEN** bot "mofidbot" named "Mofid" with a 512 by 512 PNG icon
- **WHEN** a browser asks for `/@mofidbot/manifest.webmanifest`
- **THEN** it is JSON with `start_url` "/@mofidbot", `scope` "/@mofidbot", `name` "Mofid", and one icon: the bot's, `sizes` "512x512", `purpose` "any"; no Dalan icon

#### Scenario: Real size
- **GIVEN** the bot set a 640 by 640 JPEG with `setMyProfilePhoto`
- **WHEN** a browser asks for its manifest
- **THEN** the icon has `sizes` "640x640"

#### Scenario: JPEG icon
- **GIVEN** bot "mofidbot" with a 512 by 512 JPEG icon
- **WHEN** a browser asks for its manifest
- **THEN** its one icon is `/api/bots/{id}/icon.svg`, type `image/svg+xml`, `sizes` "512x512", and that address gives an SVG that holds the JPEG

#### Scenario: Icon not fit for an app
- **GIVEN** the bot has no icon, or a 100 by 100 one, or a 600 by 400 one
- **WHEN** a browser asks for its manifest and `/@mofidbot/icon`
- **THEN** the manifest has Dalan's icons, and the icon address gives Dalan's 192 icon

#### Scenario: The bot's page links it
- **WHEN** a browser opens `/@mofidbot`
- **THEN** the page links `/@mofidbot/manifest.webmanifest`, and its `rel="icon"` and `rel="apple-touch-icon"` links are `/@mofidbot/icon`

#### Scenario: No such bot
- **WHEN** a browser asks for the manifest or the icon of a bot that does not exist or is suspended
- **THEN** the response is 404

### Requirement: REL-57 View link for an HTML file
`POST /api/bots/{bot id}/files/{file id}/view` SHALL answer `{"url", "expires_at"}` when the user may get the file (REL-29) and it is HTML. The url SHALL be `https://<view host>/v/<token>`; the view host is `Relay:FileViewHost`, else `view.<Sites:Domain>`. The token SHALL name only this user, bot and file and work for `Relay:FileViewMinutes` (start 10). Errors: 404 as REL-29, 400 `not_html`, 503 `viewer_off`. The settings (REL-18) SHALL say `file_view`. Each call SHALL be in the access log.

#### Scenario: Link for a report
- **GIVEN** a bot sent `report.html` to Sara and `Sites:Domain` is `dalans.ir`
- **WHEN** Sara asks for its view link
- **THEN** the url starts with `https://view.dalans.ir/v/`, `expires_at` is 10 minutes later, and her call is in the access log

#### Scenario: Not HTML
- **WHEN** Sara asks for the view link of a PDF
- **THEN** the answer is 400 `not_html`

#### Scenario: Not hers
- **WHEN** Ali asks for the view link of Sara's file
- **THEN** the answer is 404

#### Scenario: Viewer off
- **GIVEN** `Relay:FileViewHost` and `Sites:Domain` are empty
- **WHEN** Sara asks for a view link
- **THEN** the answer is 503 `viewer_off`

#### Scenario: The client knows
- **WHEN** the client loads its settings (REL-18)
- **THEN** `file_view` is true when a view host is set (`Relay:FileViewHost` or `Sites:Domain`), and false when both are empty, so the card shows only Download

#### Scenario: What is HTML
- **WHEN** a file has type `text/html`, or no clear type (`application/octet-stream`, empty) and a name ending `.html` or `.htm`
- **THEN** it gets a view link; any other file answers 400 `not_html`
