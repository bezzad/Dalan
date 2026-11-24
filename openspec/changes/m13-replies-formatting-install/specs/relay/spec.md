# Relay

## MODIFIED Requirements

### Requirement: REL-18 Client settings
`GET /api/settings` SHALL return the settings the client needs: `max_text_length`, `max_caption_length`, `max_file_size_mb`, `sync_page_size` (`Relay:MaxMessagesPerSync`), `reconnect_min_seconds` and `reconnect_max_seconds` (`Relay:ClientReconnect...`, start 1 and 30), `status_refresh_seconds` (`Relay:ClientStatusRefreshSeconds`, start 30), `max_bot_icon_kb` and `press_wait_seconds` (`Relay:ClientPressWaitSeconds`, start 15). Each comes from the `Relay` setting of the same name. The client SHALL NOT hard-code these values.

#### Scenario: Read the settings
- **GIVEN** `Relay:MaxTextLength` is 10 and `Relay:ClientPressWaitSeconds` is 9
- **WHEN** a logged-in user calls `GET /api/settings`
- **THEN** the response has `max_text_length` 10, `press_wait_seconds` 9, the caption and file limits, the page size, both reconnect values and `status_refresh_seconds`

### Requirement: REL-20 Inline keyboard
`POST /v1/messages` SHALL accept an optional `inline_keyboard`: rows of buttons, each with `text`, an optional `style` (`success`, `danger` or `primary`) and exactly one of `callback_data` (1 to `Relay:MaxCallbackDataBytes` UTF-8 bytes, start 64), `url` (`http` or `https`, at most `Relay:MaxButtonUrlLength`, start 2048) or `switch_query` (0 to `Relay:MaxTextLength` characters: a tap puts `@username` and this text in the user's input). Count and text follow REL-17. It SHALL be stored and returned, with `style`, in history and in the event stream. A keyboard that breaks a rule SHALL get 400 `bad_keyboard`, and nothing is stored.

#### Scenario: Bot sends inline buttons
- **WHEN** the bot posts a message with `"inline_keyboard":[[{"text":"Yes","callback_data":"y","style":"danger"},{"text":"Site","url":"https://dalanet.ir"}]]`
- **THEN** the user's history has the message with the same buttons, the first with `style` `danger`, the second with no `style`

#### Scenario: Fill button
- **WHEN** the bot posts a button `{"text":"Edit","switch_query":"todo 7 "}`
- **THEN** the user's history has the button with `switch_query` "todo 7 "

#### Scenario: Broken button
- **WHEN** a button has two of `callback_data`, `url` and `switch_query`, none, data over the limit, a `url` that is not http or https, or an unknown style
- **THEN** the response is 400 `bad_keyboard` and nothing is stored

### Requirement: REL-23 Edit a message
`PATCH /v1/messages/{user id}/{message id}` with optional `text`, optional `entities` (REL-53) and optional `inline_keyboard` SHALL change a bot message of that chat: the text when given (for a message with a file, its caption), with its entities, and the inline keyboard always (missing = removed). An edit without `text` keeps the text and its entities. It gets the next version and `edited`, is returned like REL-08, and reaches the streams (LIVE-02). Errors: 404 `message_not_found` (none, or deleted), 400 `not_editable` (user message), `not_modified` (nothing changed), and those of REL-09, REL-20 and REL-53. The caption follows the caption limit.

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

## ADDED Requirements

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
`GET /@{username}/manifest.webmanifest` SHALL need no session and return a web app manifest for that bot: `id`, `start_url` and `scope` all `/@username`, `name` and `short_name` the bot's name, `display` "standalone", and icons: the bot's icon (REL-51) when it has one, then Dalan's icons. An unknown, deleted or suspended bot SHALL get 404. The page served for `/@{username}` SHALL link this manifest instead of the main one.

#### Scenario: Manifest of a bot
- **GIVEN** bot "mofidbot" named "Mofid" with an icon
- **WHEN** a browser asks for `/@mofidbot/manifest.webmanifest`
- **THEN** it is JSON with `start_url` "/@mofidbot", `scope` "/@mofidbot", `name` "Mofid" and the bot's icon first

#### Scenario: The bot's page links it
- **WHEN** a browser opens `/@mofidbot`
- **THEN** the page links `/@mofidbot/manifest.webmanifest`

#### Scenario: No such bot
- **WHEN** a browser asks for the manifest of a bot that does not exist or is suspended
- **THEN** the response is 404
