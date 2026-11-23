# Spec Delta

## MODIFIED Requirements

### Requirement: REL-17 Reply keyboard
`POST /v1/messages` SHALL accept an optional `reply_keyboard`: a list of rows, each row a list of buttons; a button is its text, or `{"text", "style"}` where `style` is `success`, `danger` or `primary`. The optional flags `reply_keyboard_persistent`, `reply_keyboard_resize` and `reply_keyboard_one_time` (false when missing) SHALL be stored with it. The keyboard SHALL be stored with the message and returned to the user in history and in the event stream: `reply_keyboard` as rows of texts, and `reply_keyboard_options` (`persistent`, `resize`, `one_time`, `styles` as rows of a style or null) only when a flag or a style is set. Rows SHALL keep the order and size the bot sent. The limits are `Relay:MaxKeyboardButtons` (start 100) and `Relay:MaxButtonTextLength` (start 64); a keyboard over them, or an unknown style, SHALL get HTTP 400 with error code `bad_keyboard`.

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

### Requirement: REL-20 Inline keyboard
`POST /v1/messages` SHALL accept an optional `inline_keyboard`: rows of buttons, each with `text`, an optional `style` (`success`, `danger` or `primary`) and exactly one of `callback_data` (1 to `Relay:MaxCallbackDataBytes` UTF-8 bytes, start 64) or `url` (`http` or `https`, at most `Relay:MaxButtonUrlLength`, start 2048). Count and text follow REL-17. It SHALL be stored and returned, with `style`, in history and in the event stream. A keyboard that breaks a rule SHALL get 400 `bad_keyboard`, and nothing is stored.

#### Scenario: Bot sends inline buttons
- **WHEN** the bot posts a message with `"inline_keyboard":[[{"text":"Yes","callback_data":"y","style":"danger"},{"text":"Site","url":"https://dalanet.ir"}]]`
- **THEN** the user's history has the message with the same buttons, the first with `style` `danger`, the second with no `style`

#### Scenario: Broken button
- **WHEN** a button has both `callback_data` and `url`, neither, data over the limit, a `url` that is not http or https, or an unknown style
- **THEN** the response is 400 `bad_keyboard` and nothing is stored

### Requirement: REL-28 A user sends a file
`POST /api/bots/{bot id}/files` with the file as the body, its type as `Content-Type`, the header `X-File-Name` and optional `X-Caption` (both percent-encoded) SHALL send a user message that carries the file, like REL-15. Kind: with the header `X-Voice-Duration` (whole seconds, 0 or more) and an `audio/*` type, voice with that duration; else `image/jpeg|png|gif|webp` photo, `audio/*` audio, `video/*` video, else document. A bad `X-Voice-Duration` SHALL get 400 `bad_request`. The bot gets it with its `file` (REL-07). Limits: `Relay:MaxFileSizeMb` (413 `file_too_big`) and `Relay:UserUploadMbPerDay` (start 200) in 24 hours (429 `upload_limit`).

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

## ADDED Requirements

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
