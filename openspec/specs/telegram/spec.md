# telegram Specification

## Purpose
The Telegram-shaped door for bots: a bot written for Telegram works on Dalan with only the server address and the token changed. It follows Telegram Bot API 10.4 (2026-11-09) and serves only what the core can do.

## Requirements

### Requirement: TG-01 Telegram-shaped address and token
The server SHALL serve Telegram Bot API calls at `/bot<token>/<method>`, at the root of the address as Telegram does, for GET and POST. The token SHALL be the bot's Dalan token (REL-03). The method name SHALL match without regard to letter case. A wrong or malformed token SHALL get HTTP 401 with `{"ok":false,"error_code":401,"description":"Unauthorized"}`, and the call SHALL do nothing.

#### Scenario: Valid token
- **WHEN** a bot calls `GET /bot<token>/getMe` with its token
- **THEN** the response is 200 with `"ok":true`

#### Scenario: Wrong secret
- **WHEN** a call has the id of a real bot and a wrong secret
- **THEN** the response is 401 with `ok` false, `error_code` 401 and `description` "Unauthorized"

#### Scenario: Method name in another case
- **WHEN** a bot calls `/bot<token>/GETME` or `/bot<token>/getme`
- **THEN** the answer is the same as for `getMe`

#### Scenario: POST
- **WHEN** a bot calls `getMe` with POST
- **THEN** the answer is the same as with GET

### Requirement: TG-02 Answers have Telegram's shape
A call that works SHALL get HTTP 200 with `{"ok":true,"result":...}`. A call that fails SHALL get `{"ok":false,"error_code":<HTTP status>,"description":"<what went wrong>"}` with that HTTP status. JSON names are snake_case, empty fields are left out, and text that is not Latin is not escaped.

#### Scenario: Every error
- **WHEN** adapter calls fail with 400, 401, 403, 409 or 429
- **THEN** each body has `ok` false, `error_code` equal to the HTTP status and a non-empty `description`

#### Scenario: Persian text
- **GIVEN** a user named "سارا" sent "سلام"
- **WHEN** the bot calls `getUpdates`
- **THEN** the body holds "سارا" and "سلام" as they are

### Requirement: TG-03 Parameters come the way Telegram takes them
A method SHALL read its parameters from the URL query string, from an `application/x-www-form-urlencoded` or `multipart/form-data` body, or from a JSON body. A number MAY come as a string. A body that cannot be read SHALL get 400.

#### Scenario: Four ways to send one message
- **GIVEN** a user who started the bot
- **WHEN** the bot calls `sendMessage` with `chat_id` and `text` in the query string, in a URL-encoded form, in a multipart form, or in a JSON body
- **THEN** each call is 200 and stores the message

#### Scenario: Number as a string
- **WHEN** the bot posts JSON with `"chat_id":"7"`
- **THEN** the message goes to user 7

#### Scenario: Broken JSON
- **WHEN** the bot posts a body that is not valid JSON with `Content-Type: application/json`
- **THEN** the response is 400 with `ok` false

### Requirement: TG-04 getMe
`getMe` SHALL return the bot as a Telegram `User`: `id`, `is_bot` true, `first_name` (the bot's name), `username`, and `can_join_groups`, `can_read_all_group_messages`, `supports_inline_queries` all false.

#### Scenario: Who am I
- **GIVEN** a bot with username `testbot` and name "Test bot"
- **WHEN** it calls `getMe`
- **THEN** the result equals the saved example `getMe.json`

### Requirement: TG-05 getUpdates
`getUpdates` SHALL return Telegram `Update` objects of type `message`: `update_id`, and `message` with `message_id` (the sequence number in the chat), `from`, `chat` (`id` is the user's id, `type` "private"), `date` (Unix seconds) and `text`. `offset`, `limit` and `timeout` SHALL follow the queue rules Q-03, Q-05, Q-06 and Q-08. A negative `offset` counts as 0 and a `limit` under 1 as 1. Nothing in the answer SHALL hold a phone number (REL-10).

#### Scenario: One message waiting
- **GIVEN** user "Sara" with username `sara` sent "hello"
- **WHEN** the bot calls `getUpdates`
- **THEN** the result equals the saved example `update.json`

#### Scenario: Confirm with offset
- **GIVEN** the bot got update 5
- **WHEN** it calls `getUpdates` with `offset` 6
- **THEN** the result is empty and the message is marked "delivered"

#### Scenario: Negative offset
- **GIVEN** one update is waiting
- **WHEN** the bot calls `getUpdates` with `offset` -1
- **THEN** the update is returned

#### Scenario: Long poll
- **GIVEN** a `getUpdates` call with `timeout` 50 is waiting
- **WHEN** a user sends a message
- **THEN** the call returns that update at once

#### Scenario: Second poll
- **GIVEN** a `getUpdates` call is waiting
- **WHEN** a second one arrives with the same token
- **THEN** the first ends with 409 and a `description` that starts with "Conflict"

#### Scenario: No phone number
- **GIVEN** a user with phone number `+989121234567` sent a message
- **WHEN** the bot calls `getUpdates`
- **THEN** the body does not hold that number

### Requirement: TG-06 Commands are marked
A `message` whose text has commands SHALL carry `entities` with one `bot_command` entity (`offset`, `length` in UTF-16 units) for each command. A command is `/` and 1 to 32 letters, digits or `_`, with an optional `@<bot username>`, at the start of the text or after white space. A text without a command SHALL have no `entities` field.

#### Scenario: Start
- **WHEN** a user sends "/start"
- **THEN** the message has `entities` `[{"type":"bot_command","offset":0,"length":6}]`

#### Scenario: Command with the bot's name and an argument
- **WHEN** a user sends "/help@testbot now"
- **THEN** the entity has `offset` 0 and `length` 13

#### Scenario: Command after other text
- **WHEN** a user sends "سلام /menu"
- **THEN** the entity has `offset` 5 and `length` 5

#### Scenario: Not a command
- **WHEN** a user sends "hello", "1/2", "a/b" or "/"
- **THEN** the message has no `entities` field

### Requirement: TG-07 sendMessage
`sendMessage` with `chat_id` (the user's id) and `text` SHALL save the bot's message (REL-08) and return a Telegram `Message`: `message_id`, `from` (the bot), `chat` (`id`, `type` "private"), `date`, `text` and its `entities` (TG-25). A failure SHALL get the Telegram error of its kind: 400 for a bad text or chat, 403 for a user who has not started the bot or has stopped it. Parameters the adapter does not know SHALL be ignored.

#### Scenario: Answer a user
- **GIVEN** a user who started the bot
- **WHEN** the bot calls `sendMessage` with that user's id and "hello"
- **THEN** the result equals the saved example `sentMessage.json` and the user's history has the message

#### Scenario: User never started the bot
- **WHEN** the bot calls `sendMessage` for a user with no chat with it
- **THEN** the response is 403, the `description` starts with "Forbidden", and nothing is stored

#### Scenario: Empty or too long text
- **WHEN** the bot calls `sendMessage` with no text, or with a text over the limit
- **THEN** the response is 400 with "Bad Request: message text is empty", or "Bad Request: message is too long"

#### Scenario: Chat that cannot be
- **WHEN** the bot calls `sendMessage` with no `chat_id` or with `chat_id` "@channel"
- **THEN** the response is 400 "Bad Request: chat not found"

#### Scenario: Extra parameters
- **WHEN** the bot calls `sendMessage` with `disable_notification` true and `parse_mode` "MarkdownV2"
- **THEN** the response is 200 and the stored text is the text as sent

### Requirement: TG-08 Reply keyboard
`sendMessage` and the file methods SHALL accept `reply_markup` as JSON or a JSON string. `ReplyKeyboardMarkup` SHALL become the reply keyboard (REL-17), rows as sent, a button a string or `text` and `style`, with `is_persistent`, `resize_keyboard`, `one_time_keyboard`. `ReplyKeyboardRemove` removes it; `InlineKeyboardMarkup` is TG-14. `input_field_placeholder`, `selective` and `icon_custom_emoji_id` are ignored. Other markup: 400 with a `description`, nothing stored.

#### Scenario: Keyboard as an object
- **WHEN** the bot posts JSON with `"reply_markup":{"keyboard":[[{"text":"Yes"},"No"],["Cancel"]],"resize_keyboard":true}`
- **THEN** the user's history has the message with the keyboard `[["Yes","No"],["Cancel"]]` and `resize` true

#### Scenario: Style and options kept
- **WHEN** the bot sends a keyboard with rows of 5, 4 and 3 buttons, the last `{"text":"➕","style":"success"}`, with `is_persistent` and `one_time_keyboard` true
- **THEN** the user's history has the same three rows, `success` on the last button, `persistent` and `one_time` true

#### Scenario: Keyboard as a string in a form
- **WHEN** the bot posts a form whose `reply_markup` field is the same JSON as text
- **THEN** the message has the same keyboard

#### Scenario: Remove the keyboard
- **WHEN** the bot sends `"reply_markup":{"remove_keyboard":true}`
- **THEN** the message in the user's history has an empty `reply_keyboard`

#### Scenario: Markup Dalan does not have
- **WHEN** the bot sends a `force_reply`, a button with `request_contact`, or `reply_markup` that is not JSON
- **THEN** the response is 400 and nothing is stored

#### Scenario: Keyboard over the limits
- **WHEN** the bot sends a keyboard with more buttons than `Relay:MaxKeyboardButtons`, or a button with the style `pink`
- **THEN** the response is 400 and nothing is stored

### Requirement: TG-09 Other methods get a clear error
A method the adapter does not serve SHALL get 400 with `{"ok":false,"error_code":400,"description":"Bad Request: method <name> is not supported by Dalan"}`, and SHALL do nothing. `deleteWebhook` (no webhook to delete), `sendChatAction`, and `setChatMenuButton` with no `menu_button` or type `commands` or `default` SHALL answer `true` and do nothing. A `web_app` menu button SHALL get 400 "Bad Request: web_app menu button is not supported by Dalan".

#### Scenario: Method Dalan does not have yet
- **WHEN** a bot calls `sendSticker`, `setWebhook` or `noSuchMethod`
- **THEN** the response is 400 and the `description` names the method and says "not supported by Dalan"

#### Scenario: Start of polling
- **WHEN** a bot calls `deleteWebhook`
- **THEN** the response is 200 with `"result":true`

#### Scenario: Typing
- **WHEN** a bot calls `sendChatAction` with `chat_id` and `action` "typing"
- **THEN** the response is 200 with `"result":true`

#### Scenario: Commands menu button
- **WHEN** a bot calls `setChatMenuButton` with `menu_button` `{"type":"commands"}`
- **THEN** the response is 200 with `"result":true`

#### Scenario: Mini App menu button
- **WHEN** a bot calls `setChatMenuButton` with `menu_button` of type `web_app`
- **THEN** the response is 400 and the `description` says "not supported by Dalan"

### Requirement: TG-10 The token never reaches the log
The server SHALL NOT write a bot token, or a request path that holds one, to its log.

#### Scenario: Calls that work and calls that fail
- **WHEN** a bot makes adapter calls that succeed, a call with a bad body and a call to an unknown method
- **THEN** no log line holds the secret of its token

### Requirement: TG-11 The limit per bot holds here too
Adapter calls SHALL count against the same limit per bot as the core Bot API (SEC-01). A call over it SHALL get 429 with `description` "Too Many Requests: retry after <seconds>", `parameters.retry_after` and a `Retry-After` header.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 calls per window
- **WHEN** a bot makes 3 adapter calls in the same window
- **THEN** the third is 429 with `parameters.retry_after` above 0 and a `Retry-After` header

#### Scenario: One limit for both doors
- **GIVEN** the limit is 2 calls per window
- **WHEN** a bot makes one core call, one adapter call and then another adapter call
- **THEN** the third is 429

### Requirement: TG-12 A real Telegram library works
A stock Telegram client library SHALL work against Dalan with only its base address and token changed.

#### Scenario: Telegram.Bot for .NET
- **GIVEN** the `Telegram.Bot` client with base address `<server>` and the bot's Dalan token
- **WHEN** it calls `GetMe`, reads the "/start" of a user with `GetUpdates`, and answers with `SendMessage` and a `ReplyKeyboardMarkup`
- **THEN** every call works, the update has the user and the text, and the user's history has the answer with its keyboard

#### Scenario: Rich calls with Telegram.Bot
- **GIVEN** the same client
- **WHEN** it sets commands, sends a message with an `InlineKeyboardMarkup`, reads the user's press as a `CallbackQuery`, answers it, edits the message, pins it, sends a photo, downloads a user's file and deletes a message
- **THEN** every call works and the library reads every answer

### Requirement: TG-13 Adapter calls are in the access log
Every adapter call with a valid token SHALL be recorded in the access log as that bot, with the real client address and port (ADM-01, SEC-10), in the same way as a core Bot API call. A call with a wrong token SHALL write no row.

#### Scenario: A Telegram call
- **WHEN** a bot calls `getMe` through the adapter from address `203.0.113.7` port `51000`
- **THEN** the access log has one row for that bot with that address and port

#### Scenario: A faked header from the client
- **WHEN** a client that is not a trusted proxy sends `X-Forwarded-For: 9.9.9.9` with its adapter call
- **THEN** the row has the address of the connection, not `9.9.9.9`

#### Scenario: Wrong token
- **WHEN** an adapter call has a wrong token
- **THEN** the access log has no row

### Requirement: TG-14 Inline keyboard and button presses
An `InlineKeyboardMarkup` (buttons: `text`, optional `style`, and `callback_data`, `url` or a `switch_inline_query*` kind) SHALL become the inline keyboard (REL-20); a switch kind a `switch_query` button with its query (empty when missing); other kinds 400. A sent or edited `Message` SHALL carry it in `reply_markup` with `style`, a switch as `switch_inline_query_current_chat`. A press (REL-21) SHALL reach `getUpdates` as `callback_query`: `id`, `from`, `message`, `chat_instance`, `data`.

#### Scenario: Press
- **GIVEN** the bot sent a message with a button whose `callback_data` is "y", and the user pressed it
- **WHEN** the bot calls `getUpdates`
- **THEN** the result equals the saved example `callbackQuery.json`

#### Scenario: Style kept
- **WHEN** the bot sends an inline button `{"text":"🗑","callback_data":"d","style":"danger"}`
- **THEN** the returned `Message` and the user's history have the button with `style` `danger`

#### Scenario: Switch button
- **WHEN** the bot edits a message with a button `{"text":"✏️","switch_inline_query_current_chat":"\nBuy milk"}`
- **THEN** the edit succeeds, the returned `reply_markup` has that button, and the user's history has it with `switch_query` "\nBuy milk"

#### Scenario: Button kind Dalan does not have
- **WHEN** the bot sends an inline button with `login_url`
- **THEN** the response is 400 and nothing is stored

### Requirement: TG-15 answerCallbackQuery
`answerCallbackQuery` with `callback_query_id`, optional `text` and `show_alert` SHALL answer the press (REL-22) and return `true`. An unknown or old id SHALL get 400 "Bad Request: query is too old and response timeout expired or query ID is invalid". A `url` SHALL get 400.

#### Scenario: Answer
- **WHEN** the bot answers a fresh press with "Saved"
- **THEN** the result is `true` and the user's stream gets the answer

#### Scenario: Unknown id
- **WHEN** the bot answers id "999"
- **THEN** the response is 400 and the `description` starts with "Bad Request: query is too old"

### Requirement: TG-16 Edit a message
`editMessageText` (`text`, `entities` or `parse_mode`), `editMessageCaption` (`caption`, `caption_entities` or `parse_mode`) and `editMessageReplyMarkup`, with `chat_id`, `message_id`, `reply_markup`, SHALL edit the message (REL-23), returned with `edit_date`. Errors: Telegram's "Bad Request: " texts: "message to edit not found", "message can't be edited", "there is no text in the message to edit", "there is no caption in the message to edit", "message is not modified". `inline_message_id`: 400.

#### Scenario: Edit text
- **WHEN** the bot calls `editMessageText` for its message 2 with "done"
- **THEN** the result is the message with text "done" and an `edit_date`

#### Scenario: Edit caption
- **GIVEN** bot message 2 is a photo with caption "cat"
- **WHEN** the bot calls `editMessageCaption` with caption "dog"
- **THEN** the result has the photo and `caption` "dog"

#### Scenario: Wrong kind
- **WHEN** the bot calls `editMessageCaption` for a text message, or `editMessageText` for a photo
- **THEN** the response is 400 "Bad Request: there is no caption in the message to edit", or "Bad Request: there is no text in the message to edit"

#### Scenario: Same text
- **WHEN** the bot edits message 2 with the text it already has and no markup
- **THEN** the response is 400 and the `description` starts with "Bad Request: message is not modified"

### Requirement: TG-17 Delete a message
`deleteMessage` (`chat_id`, `message_id`) SHALL delete the message (REL-24) and return `true`; a missing one SHALL get 400 "Bad Request: message to delete not found". `deleteMessages` (`chat_id`, `message_ids`: a JSON list of 1 to 100 ids) SHALL delete each message it finds, skip the missing ones, and return `true`; a list that is missing, empty, longer than 100 or not numbers SHALL get 400 and delete nothing.

#### Scenario: Delete
- **WHEN** the bot deletes message 2
- **THEN** the result is `true` and message 2 is marked deleted

#### Scenario: Delete several
- **GIVEN** the bot sent messages 2 and 3
- **WHEN** the bot calls `deleteMessages` with `message_ids` `[2, 3, 99]`
- **THEN** the result is `true` and messages 2 and 3 are marked deleted

#### Scenario: Bad list
- **WHEN** the bot calls `deleteMessages` with `message_ids` `[]`
- **THEN** the response is 400

### Requirement: TG-18 Pin and unpin
`pinChatMessage` (`chat_id`, `message_id`) SHALL pin the message (REL-25); `unpinChatMessage` and `unpinAllChatMessages` (`chat_id`) SHALL unpin it. Each returns `true`. A missing message SHALL get 400 "Bad Request: message to pin not found".

#### Scenario: Pin and unpin
- **WHEN** the bot pins message 2 and then calls `unpinAllChatMessages`
- **THEN** both results are `true` and the chat has no pinned message

### Requirement: TG-19 Commands
`setMyCommands` (`commands`), `getMyCommands` and `deleteMyCommands` SHALL set, read and clear the command list (REL-26) and answer as Telegram does. A call with a `language_code`, or a `scope` other than `default` or `all_private_chats`, SHALL answer `true` (or an empty list for `getMyCommands`) and change nothing.

#### Scenario: Set and get
- **WHEN** the bot sets `[{"command":"start","description":"Start"}]` and then calls `getMyCommands`
- **THEN** the result is that list

#### Scenario: Another language
- **WHEN** the bot sets a list with `language_code` "en"
- **THEN** the result is `true` and the stored list did not change

### Requirement: TG-20 Send files
`sendPhoto`, `sendDocument`, `sendAudio`, `sendVoice`, `sendVideo` SHALL send that kind of file (REL-27) with optional `caption`, `reply_markup` and, for voice, audio, video, `duration` (REL-50). The file SHALL be an uploaded part or a `file_id`; a URL gets 400. The `Message` SHALL carry it in Telegram's shape: `photo` a list of one `PhotoSize`, others one object, with `file_id`, `file_unique_id`, `file_size` and Telegram's other fields (`width`, `height` 0; `duration` the stored length or 0).

#### Scenario: Upload a photo
- **WHEN** the bot calls `sendPhoto` with a JPEG part and caption "cat"
- **THEN** the result equals the saved example `sentPhoto.json` (ids aside) and the user's history has the photo

#### Scenario: Voice with a length
- **WHEN** the bot calls `sendVoice` with an OGG part and `duration` 5
- **THEN** the result has `voice` with `duration` 5 and `mime_type` `audio/ogg`

#### Scenario: URL
- **WHEN** the bot calls `sendDocument` with `document` "https://example.com/a.pdf"
- **THEN** the response is 400 and nothing is stored

#### Scenario: Bad file
- **WHEN** the file is an unknown `file_id`, or bigger than the limit
- **THEN** the response is 400 "Bad Request: wrong file identifier/HTTP URL specified", or 413 "Request Entity Too Large"

### Requirement: TG-21 Files from users and downloads
A user's file message SHALL reach `getUpdates` with the file in Telegram's shape (TG-20) and `caption`; a voice (REL-28) as `message.voice` with `file_id`, `file_unique_id`, `duration`, `mime_type`, `file_size`. `getFile` (`file_id`) SHALL return a `File` with `file_id`, `file_unique_id`, `file_size`, `file_path`; `GET /file/bot<token>/<file_path>` SHALL return the bytes (REL-29). An unknown file: 400 "Bad Request: invalid file_id" from `getFile`, 404 from the download.

#### Scenario: Download a user's photo
- **GIVEN** a user sent a photo
- **WHEN** the bot calls `getFile` with its `file_id` and downloads `file_path`
- **THEN** it gets the same bytes

#### Scenario: A user's voice
- **GIVEN** a user recorded a voice of 7 seconds as `audio/ogg`
- **WHEN** the bot calls `getUpdates`, then `getFile` and the download
- **THEN** the update equals the saved example `userVoice.json` (ids aside) and the bytes are the same

### Requirement: TG-22 Suspended bot
A Telegram adapter call of a suspended bot (REL-38) SHALL get HTTP 403 with `{"ok":false,"error_code":403,"description":"Forbidden: bot was suspended"}` and SHALL do nothing.

#### Scenario: getMe
- **GIVEN** a suspended bot
- **WHEN** it calls `getMe`
- **THEN** the answer is 403 with that description

### Requirement: TG-26 A moved chat in Telegram's shape
The Telegram door SHALL show a `chat_moved` update (REL-56) as a `message` in the old chat with `migrate_to_chat_id` set to the new chat id, and a `chat_moved` error as 400 "Bad Request: group chat was upgraded to a supergroup chat" with `parameters.migrate_to_chat_id`, the way Telegram tells a bot that a group moved.

#### Scenario: Update
- **GIVEN** chat 9 of bot 5 moved to chat 4
- **WHEN** the bot calls `getUpdates` on the Telegram door
- **THEN** it gets a message with `chat.id` 9 and `migrate_to_chat_id` 4

#### Scenario: Error
- **WHEN** the bot calls `sendMessage` with `chat_id` 9 on the Telegram door
- **THEN** the answer is `ok` false, `error_code` 400, and `parameters.migrate_to_chat_id` 4

### Requirement: TG-23 Bot profile photo
`setMyProfilePhoto` SHALL take `photo`, an `InputProfilePhoto` of type `static` with `photo` `attach://<part>`, set the bot icon (REL-51) and return `true`. Type `animated` SHALL get 400 "Bad Request: animated profile photos are not supported by Dalan"; not PNG, JPEG or WebP, 400 "Bad Request: IMAGE_PROCESS_FAILED"; over the limit, 400 "Bad Request: photo is too big". `removeMyProfilePhoto` SHALL remove the icon and return `true`.

#### Scenario: Set the photo
- **WHEN** the bot calls `setMyProfilePhoto` with `{"type":"static","photo":"attach://p"}` and a JPEG part `p`
- **THEN** the result is `true` and `GET /api/bots/{id}/icon` gives the same bytes

#### Scenario: Remove the photo
- **GIVEN** the bot has an icon
- **WHEN** it calls `removeMyProfilePhoto`
- **THEN** the result is `true` and the bot has no icon

#### Scenario: Not a picture
- **WHEN** the bot sends a text file as the photo
- **THEN** the response is 400 and the old icon stays

### Requirement: TG-24 Replies
The send methods SHALL take `reply_parameters` (`message_id`, `allow_sending_without_reply`) or the older `reply_to_message_id`, and send a reply (REL-52). A missing target SHALL get 400 "Bad Request: message to be replied not found", unless `allow_sending_without_reply` is true: then it is sent without the reply. A `Message` that replies SHALL carry `reply_to_message`: the copy, with `from` the bot or the user (empty `first_name` in a send answer), its text or caption and file.

#### Scenario: User replies
- **GIVEN** the bot's message 2 "Which session?" and the user's reply to it
- **WHEN** the bot calls `getUpdates`
- **THEN** the message has `reply_to_message` with `message_id` 2, `from.is_bot` true and `text` "Which session?"

#### Scenario: Bot replies
- **GIVEN** user message 3
- **WHEN** the bot calls `sendMessage` with `reply_parameters` `{"message_id":3}`
- **THEN** the result has `reply_to_message.message_id` 3 and the user's history shows the reply

#### Scenario: Target gone
- **WHEN** the bot replies to message 99 that does not exist, once plain and once with `allow_sending_without_reply` true
- **THEN** the first gets 400 "Bad Request: message to be replied not found", the second is sent with no `reply_to_message`

### Requirement: TG-25 Formatting
`parse_mode` "HTML" SHALL turn Telegram's tags and character references into text and entities (REL-53); tag list in design.md, decision 4. A link that is not http(s) SHALL keep only its text. An unknown or broken tag SHALL get 400 "Bad Request: can't parse entities…". Without it, `entities` or `caption_entities` SHALL be taken as given, kinds Dalan does not draw left out. "MarkdownV2" and "Markdown" SHALL keep the text as sent. A returned `Message` SHALL carry `entities` or `caption_entities`.

#### Scenario: Bold and link
- **WHEN** the bot calls `sendMessage` with text `<b>Buy</b> &amp; <a href="https://dalanet.ir">see</a>` and `parse_mode` "HTML"
- **THEN** the stored text is "Buy & see" with a `bold` entity at 0 length 3 and a `text_link` at 6 length 3, and the result has the same `entities`

#### Scenario: Unknown tag
- **WHEN** the bot sends `a<br>b` with `parse_mode` "HTML"
- **THEN** the response is 400, the `description` starts with "Bad Request: can't parse entities", and nothing is stored

#### Scenario: Entities as given
- **WHEN** the bot calls `sendPhoto` with caption "cat" and `caption_entities` `[{"type":"italic","offset":0,"length":3}]`
- **THEN** the result has `caption_entities` with that entity
