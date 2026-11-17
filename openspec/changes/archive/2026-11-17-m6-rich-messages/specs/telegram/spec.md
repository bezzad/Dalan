## MODIFIED Requirements

### Requirement: TG-08 Reply keyboard
`sendMessage` and the file methods SHALL accept `reply_markup` as a JSON object or as a string of JSON. A `ReplyKeyboardMarkup` SHALL become the reply keyboard of the message (REL-17); a button is a string or an object with `text`. A `ReplyKeyboardRemove` SHALL remove the keyboard. An `InlineKeyboardMarkup` SHALL become the inline keyboard (TG-14). Look-only fields SHALL be ignored. Markup that Dalan cannot serve, or cannot read, SHALL get 400 with a `description` that says what is wrong, and nothing SHALL be stored.

#### Scenario: Keyboard as an object
- **WHEN** the bot posts JSON with `"reply_markup":{"keyboard":[[{"text":"Yes"},"No"],["Cancel"]],"resize_keyboard":true}`
- **THEN** the user's history has the message with the keyboard `[["Yes","No"],["Cancel"]]`

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
- **WHEN** the bot sends a keyboard with more buttons than `Relay:MaxKeyboardButtons`
- **THEN** the response is 400 and nothing is stored

### Requirement: TG-09 Other methods get a clear error
A method the adapter does not serve SHALL get 400 with `{"ok":false,"error_code":400,"description":"Bad Request: method <name> is not supported by Dalan"}`, and SHALL do nothing. `deleteWebhook` SHALL answer `true`, because Dalan has no webhook to delete. `sendChatAction` SHALL answer `true` and do nothing.

#### Scenario: Method Dalan does not have yet
- **WHEN** a bot calls `sendSticker`, `setWebhook` or `noSuchMethod`
- **THEN** the response is 400 and the `description` names the method and says "not supported by Dalan"

#### Scenario: Start of polling
- **WHEN** a bot calls `deleteWebhook`
- **THEN** the response is 200 with `"result":true`

#### Scenario: Typing
- **WHEN** a bot calls `sendChatAction` with `chat_id` and `action` "typing"
- **THEN** the response is 200 with `"result":true`

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

## ADDED Requirements

### Requirement: TG-14 Inline keyboard and button presses
An `InlineKeyboardMarkup` (`inline_keyboard`: rows of buttons with `text` and `callback_data` or `url`) SHALL become the inline keyboard of the message (REL-20). Other inline button kinds SHALL get 400. A sent or edited `Message` SHALL carry `reply_markup` with the inline keyboard. A press (REL-21) SHALL reach `getUpdates` as an `Update` with `callback_query`: `id` (text), `from`, `message` (the bot message), `chat_instance` and `data`.

#### Scenario: Press
- **GIVEN** the bot sent a message with a button whose `callback_data` is "y", and the user pressed it
- **WHEN** the bot calls `getUpdates`
- **THEN** the result equals the saved example `callbackQuery.json`

#### Scenario: Button kind Dalan does not have
- **WHEN** the bot sends an inline button with `switch_inline_query`
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
`editMessageText` (`chat_id`, `message_id`, `text`, optional `reply_markup`) and `editMessageReplyMarkup` (`chat_id`, `message_id`, optional `reply_markup`) SHALL edit the message (REL-23) and return the edited `Message` with `edit_date`. Errors SHALL use Telegram's texts: "Bad Request: message to edit not found", "Bad Request: message can't be edited", "Bad Request: there is no text in the message to edit", "Bad Request: message is not modified". `inline_message_id` SHALL get 400.

#### Scenario: Edit text
- **WHEN** the bot calls `editMessageText` for its message 2 with "done"
- **THEN** the result is the message with text "done" and an `edit_date`

#### Scenario: Same text
- **WHEN** the bot edits message 2 with the text it already has and no markup
- **THEN** the response is 400 and the `description` starts with "Bad Request: message is not modified"

### Requirement: TG-17 Delete a message
`deleteMessage` (`chat_id`, `message_id`) SHALL delete the message (REL-24) and return `true`; a missing one SHALL get 400 "Bad Request: message to delete not found".

#### Scenario: Delete
- **WHEN** the bot deletes message 2
- **THEN** the result is `true` and message 2 is marked deleted

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
`sendPhoto`, `sendDocument`, `sendAudio`, `sendVoice` and `sendVideo` SHALL send a file of that kind (REL-27) with optional `caption` and `reply_markup`. The file field SHALL be an uploaded part or a `file_id`; a URL SHALL get 400. The returned `Message` SHALL carry the file in Telegram's shape: `photo` as a list with one `PhotoSize`, the others as one object, with `file_id`, `file_unique_id`, `file_size` and Telegram's other fields (`width`, `height`, `duration` are 0).

#### Scenario: Upload a photo
- **WHEN** the bot calls `sendPhoto` with a JPEG part and caption "cat"
- **THEN** the result equals the saved example `sentPhoto.json` (ids aside) and the user's history has the photo

#### Scenario: URL
- **WHEN** the bot calls `sendDocument` with `document` "https://example.com/a.pdf"
- **THEN** the response is 400 and nothing is stored

#### Scenario: Bad file
- **WHEN** the file is an unknown `file_id`, or bigger than the limit
- **THEN** the response is 400 "Bad Request: wrong file identifier/HTTP URL specified", or 413 "Request Entity Too Large"

### Requirement: TG-21 Files from users and downloads
A user's file message SHALL reach `getUpdates` with the file in Telegram's shape (TG-20) and the caption in `caption`. `getFile` (`file_id`) SHALL return a `File` with `file_id`, `file_unique_id`, `file_size` and `file_path`. `GET /file/bot<token>/<file_path>` SHALL return the bytes (REL-29). An unknown file SHALL get 400 "Bad Request: invalid file_id" from `getFile` and 404 from the download.

#### Scenario: Download a user's photo
- **GIVEN** a user sent a photo
- **WHEN** the bot calls `getFile` with its `file_id` and downloads `file_path`
- **THEN** it gets the same bytes
