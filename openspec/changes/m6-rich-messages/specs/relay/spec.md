## MODIFIED Requirements

### Requirement: REL-07 Get updates
`GET /v1/updates` SHALL return `{"updates":[...]}`. Each update SHALL have `update_id` and either a `message` or a `callback` (REL-21). A `message` has `id` (the sequence number in the chat), `from` (`id`, `name`, `username` of the user), `text`, `date` (UTC) and, when it carries one, `file` (REL-28).

#### Scenario: One message waiting
- **GIVEN** user 7 named "Sara" sent "/start"
- **WHEN** the bot calls `GET /v1/updates`
- **THEN** the response is 200 with one update whose message has `from.id` 7, `from.name` "Sara" and `text` "/start"

#### Scenario: Bad parameter
- **WHEN** the bot calls `GET /v1/updates?limit=abc`
- **THEN** the response is 400 with error code `bad_request`

### Requirement: REL-16 History and sync
`GET /api/bots/{bot id}/messages` SHALL return `{"messages":[...],"pinned":...}` from the chat of this user and that bot only, in rising `id` order, or rising `version` order with `after`. Each message has `id`, `version`, `from` ("user" or "bot"), `text`, `date` and, when set, `reply_keyboard`, `inline_keyboard`, `file`, `edited` (time of the last edit), `pinned` (true) and `deleted` (true). `pinned` is the pinned message of the chat, or null. The parameter `after` returns the messages whose `version` is above it; `before` returns messages whose `id` is below it; `limit` sets the size. The page size is never above `Relay:MaxMessagesPerSync` (start 100).

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

## ADDED Requirements

### Requirement: REL-20 Inline keyboard
`POST /v1/messages` SHALL accept an optional `inline_keyboard`: rows of buttons, each with `text` and exactly one of `callback_data` (1 to `Relay:MaxCallbackDataBytes` UTF-8 bytes, start 64) or `url` (`http` or `https`, at most `Relay:MaxButtonUrlLength` characters, start 2048). Button count and text follow the reply keyboard limits (REL-17). It SHALL be stored with the message and returned in history and in the event stream. A keyboard that breaks a rule SHALL get 400 `bad_keyboard` and nothing is stored.

#### Scenario: Bot sends inline buttons
- **WHEN** the bot posts a message with `"inline_keyboard":[[{"text":"Yes","callback_data":"y"},{"text":"Site","url":"https://dalanet.ir"}]]`
- **THEN** the user's history has the message with the same buttons

#### Scenario: Broken button
- **WHEN** a button has both `callback_data` and `url`, neither, data over the limit, or a `url` that is not http or https
- **THEN** the response is 400 `bad_keyboard` and nothing is stored

### Requirement: REL-21 A user presses an inline button
`POST /api/bots/{bot id}/callbacks` with `{"message_id":N,"data":"..."}` SHALL put a `callback` update in the bot's queue when message N of the user's chat is a bot message, not deleted, with a button whose `callback_data` is exactly that data, and SHALL return `{"id":"..."}`. Otherwise the answer SHALL be 404 `button_not_found` and nothing is queued. The update has `update_id` and `callback` with `id` (text), `from`, `message` (the bot message with its buttons) and `data`. A press SHALL NOT be stored in the chat history. It follows the queue rules (Q-02 to Q-06, Q-08), but is not kept over a restart.

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
`PATCH /v1/messages/{user id}/{message id}` with optional `text` and optional `inline_keyboard` SHALL change a bot message of that chat: the text when given, and the inline keyboard always (missing = removed). The message gets the next version of the chat and `edited`, is returned like REL-08, and reaches the user's streams (LIVE-02). Errors: 404 `message_not_found` (no such message, or deleted), 400 `not_editable` (a user's message), 400 `no_text` (text for a file message), 400 `not_modified` (same text and same buttons), and the text and keyboard errors of REL-09 and REL-20.

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
`POST /v1/messages` as `multipart/form-data` with the part `file`, `user_id`, optional `kind` (`photo`, `document`, `audio`, `voice` or `video`, start `document`), `text` (the caption, may be empty, at most `Relay:MaxCaptionLength`, start 1024) and the keyboards as JSON text SHALL save the file and send a message that carries it. Instead of the part, `file_id` SHALL send again a file this bot already has (sent or received in any of its chats). The message has `file` with `id`, `kind`, `name`, `type` (content type) and `size`. A file over `Relay:MaxFileSizeMb` (start 20) SHALL get 413 `file_too_big`; an unknown `file_id` 400 `file_not_found`; nothing is stored then.

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
`POST /api/bots/{bot id}/files` with the file as the body, its type as `Content-Type`, the header `X-File-Name` and optional `X-Caption` (both percent-encoded UTF-8) SHALL save the file and send a user message that carries it, like REL-15. The kind comes from the type: `image/jpeg`, `image/png`, `image/gif`, `image/webp` photo; `audio/*` audio; `video/*` video; anything else document. The bot gets the message with its `file` (REL-07). Limits: `Relay:MaxFileSizeMb` per file (413 `file_too_big`) and `Relay:UserUploadMbPerDay` (start 200) per user in 24 hours (429 `upload_limit`).

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
