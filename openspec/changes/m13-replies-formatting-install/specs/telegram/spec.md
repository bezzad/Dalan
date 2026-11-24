# Telegram adapter

## MODIFIED Requirements

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

### Requirement: TG-14 Inline keyboard and button presses
An `InlineKeyboardMarkup` (`inline_keyboard`: rows of buttons with `text`, an optional `style`, and `callback_data`, `url`, `switch_inline_query`, `switch_inline_query_current_chat` or `switch_inline_query_chosen_chat`) SHALL become the inline keyboard of the message (REL-20). The three switch kinds SHALL become a `switch_query` button with their query (`switch_inline_query_chosen_chat.query`, empty when missing). Other inline button kinds SHALL get 400. A sent or edited `Message` SHALL carry `reply_markup` with the inline keyboard, `style` included, and a switch button as `switch_inline_query_current_chat`. A press (REL-21) SHALL reach `getUpdates` as an `Update` with `callback_query`: `id` (text), `from`, `message` (the bot message), `chat_instance` and `data`.

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

### Requirement: TG-16 Edit a message
`editMessageText` (`chat_id`, `message_id`, `text`, optional `entities` or `parse_mode`, optional `reply_markup`), `editMessageCaption` (`chat_id`, `message_id`, optional `caption`, `caption_entities` or `parse_mode`, optional `reply_markup`) and `editMessageReplyMarkup` (`chat_id`, `message_id`, optional `reply_markup`) SHALL edit the message (REL-23) and return the edited `Message` with `edit_date`. Errors SHALL use Telegram's texts: "Bad Request: message to edit not found", "Bad Request: message can't be edited", "Bad Request: there is no text in the message to edit", "Bad Request: there is no caption in the message to edit", "Bad Request: message is not modified". `inline_message_id` SHALL get 400.

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

## ADDED Requirements

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
