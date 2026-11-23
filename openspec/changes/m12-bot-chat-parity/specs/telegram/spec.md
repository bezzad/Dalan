# Spec Delta

## MODIFIED Requirements

### Requirement: TG-08 Reply keyboard
`sendMessage` and the file methods SHALL accept `reply_markup` as a JSON object or as a string of JSON. A `ReplyKeyboardMarkup` SHALL become the reply keyboard (REL-17) with its rows as sent; a button is a string or an object with `text` and an optional `style` (`success`, `danger`, `primary`). `is_persistent`, `resize_keyboard` and `one_time_keyboard` SHALL be kept with it. `ReplyKeyboardRemove` SHALL remove it. `InlineKeyboardMarkup` SHALL become the inline keyboard (TG-14). Look-only fields (`input_field_placeholder`, `selective`, `icon_custom_emoji_id`) SHALL be ignored. Markup Dalan cannot serve or read SHALL get 400 with a `description` that says what is wrong, and nothing is stored.

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

### Requirement: TG-14 Inline keyboard and button presses
An `InlineKeyboardMarkup` (`inline_keyboard`: rows of buttons with `text`, an optional `style`, and `callback_data` or `url`) SHALL become the inline keyboard of the message (REL-20). Other inline button kinds SHALL get 400. A sent or edited `Message` SHALL carry `reply_markup` with the inline keyboard, `style` included. A press (REL-21) SHALL reach `getUpdates` as an `Update` with `callback_query`: `id` (text), `from`, `message` (the bot message), `chat_instance` and `data`.

#### Scenario: Press
- **GIVEN** the bot sent a message with a button whose `callback_data` is "y", and the user pressed it
- **WHEN** the bot calls `getUpdates`
- **THEN** the result equals the saved example `callbackQuery.json`

#### Scenario: Style kept
- **WHEN** the bot sends an inline button `{"text":"🗑","callback_data":"d","style":"danger"}`
- **THEN** the returned `Message` and the user's history have the button with `style` `danger`

#### Scenario: Button kind Dalan does not have
- **WHEN** the bot sends an inline button with `switch_inline_query`
- **THEN** the response is 400 and nothing is stored

### Requirement: TG-20 Send files
`sendPhoto`, `sendDocument`, `sendAudio`, `sendVoice` and `sendVideo` SHALL send a file of that kind (REL-27) with optional `caption`, `reply_markup` and, for voice, audio and video, `duration` (REL-50). The file field SHALL be an uploaded part or a `file_id`; a URL SHALL get 400. The returned `Message` SHALL carry the file in Telegram's shape: `photo` as a list with one `PhotoSize`, the others as one object, with `file_id`, `file_unique_id`, `file_size` and Telegram's other fields (`width` and `height` are 0; `duration` is the stored length, else 0).

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
A user's file message SHALL reach `getUpdates` with the file in Telegram's shape (TG-20) and the caption in `caption`. A user's voice (REL-28) SHALL be `message.voice` with `file_id`, `file_unique_id`, `duration`, `mime_type` and `file_size`. `getFile` (`file_id`) SHALL return a `File` with `file_id`, `file_unique_id`, `file_size` and `file_path`. `GET /file/bot<token>/<file_path>` SHALL return the bytes (REL-29). An unknown file SHALL get 400 "Bad Request: invalid file_id" from `getFile` and 404 from the download.

#### Scenario: Download a user's photo
- **GIVEN** a user sent a photo
- **WHEN** the bot calls `getFile` with its `file_id` and downloads `file_path`
- **THEN** it gets the same bytes

#### Scenario: A user's voice
- **GIVEN** a user recorded a voice of 7 seconds as `audio/ogg`
- **WHEN** the bot calls `getUpdates`, then `getFile` and the download
- **THEN** the update equals the saved example `userVoice.json` (ids aside) and the bytes are the same

## ADDED Requirements

### Requirement: TG-23 Bot profile photo
`setMyProfilePhoto` SHALL take `photo`, an `InputProfilePhoto` of type `static` whose `photo` is `attach://<part name>` of a multipart upload, and set the bot icon (REL-51) with its checks; it returns `true`. Type `animated` SHALL get 400 "Bad Request: animated profile photos are not supported by Dalan". `removeMyProfilePhoto` SHALL remove the icon and return `true`. A picture REL-51 refuses SHALL get 400 "Bad Request: IMAGE_PROCESS_FAILED" when it is not PNG, JPEG or WebP, and 400 "Bad Request: photo is too big" when over the limit.

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
