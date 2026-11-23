# Spec Delta

## MODIFIED Requirements

### Requirement: CLI-07 Reply buttons
The client SHALL show the reply keyboard of the newest bot message that carries one under the input bar, where a phone keyboard would be, on phone and desktop widths. An empty keyboard removes the buttons. Each row SHALL keep the buttons the bot sent in it, on one line, sharing the row width equally; rows are never joined or split. A button with a `style` SHALL have its colour (CLI-13). A tap on a button SHALL send its text as a message from the user. With `resize` false the keyboard SHALL take a fixed height that its rows share; with `resize` true each row is one button high. With `one_time` the keyboard SHALL hide after a tap. Unless `persistent`, a ⌨ button in the input bar SHALL hide and show it; a hidden keyboard comes back with the next bot message that carries one.

#### Scenario: Keyboard from the newest message that has one
- **GIVEN** bot message 2 has a keyboard and bot message 4 has none
- **WHEN** the chat is shown
- **THEN** the buttons of message 2 are shown

#### Scenario: Keyboard removed
- **GIVEN** bot message 2 has a keyboard and bot message 4 has an empty keyboard
- **WHEN** the chat is shown
- **THEN** no buttons are shown

#### Scenario: Rows as sent
- **GIVEN** the keyboard has rows of 5, 4 and 3 buttons, the last with style `success`
- **WHEN** the chat is shown at 360 and at 1280 pixels wide
- **THEN** there are three rows of 5, 4 and 3 buttons under the input, the buttons of the row of 3 are wider than those of the row of 5, and the last is green

#### Scenario: One-time keyboard
- **GIVEN** the keyboard has `one_time` true
- **WHEN** the user taps a button
- **THEN** its text is sent, the keyboard is hidden and the ⌨ button shows it again

#### Scenario: Persistent keyboard
- **WHEN** the keyboard has `persistent` true
- **THEN** there is no ⌨ button and the keyboard stays

### Requirement: CLI-13 Inline buttons
The client SHALL show the inline keyboard of a message under that message, each row as sent. A tap on a button with `callback_data` SHALL send the press (REL-21); a tap on a `url` button SHALL open the link in a new tab without the site as referrer, and the button SHALL have a small ↗ mark at its top end corner. A button's `style` SHALL colour it in both themes: `success` green (`--brand`), `danger` red (`--danger`), `primary` blue (`--primary`), with readable text. An `answer` event with a text SHALL be shown: as an alert when `show_alert` is true, otherwise as a short note.

#### Scenario: Buttons of a message
- **GIVEN** message 2 has the inline buttons `[[A],[B]]`
- **WHEN** the chat is shown
- **THEN** message 2 has two rows of buttons under it

#### Scenario: Link mark and colour
- **GIVEN** a message has a `url` button and a `callback_data` button with style `danger`
- **WHEN** the chat is shown in light and in dark
- **THEN** the link button has the ↗ mark, and the other is red with text that passes the WCAG AA contrast check

### Requirement: CLI-16 Files in the chat
The client SHALL let the user pick a file with 📎, paste it, or drop it (CLI-47), show it in a card before sending, and send it with the card's text as its caption (REL-28). A file over `max_file_size_mb` (REL-18) SHALL be refused before sending. A message with a `file` SHALL show a photo as an image, a voice or audio with the Dalan player (play and pause, a seek bar, the time and the length), video with a video player, and anything else as a link with its name and size.

#### Scenario: Kind of view
- **WHEN** messages with a photo, a voice, a video and a document are shown
- **THEN** they use an image, the Dalan player, a video player and a link

#### Scenario: Voice length
- **GIVEN** a voice with `duration` 65
- **WHEN** it is shown and not playing
- **THEN** it says 1:05

#### Scenario: Too big
- **WHEN** the user picks a file over the limit
- **THEN** nothing is sent and a message says the file is too big

### Requirement: CLI-23 Builder request in the account screen
The account screen (`/account`) and the My bots screen (`/bots`) SHALL show the user's builder state when the user is not a builder: a button "become a builder" when it is `none`, "waiting for approval" when `requested`. A user with no phone SHALL see, instead of the request button, a note that building needs a verified mobile number, with a button to verify it (CLI-34). For a builder or an admin the account screen SHALL have a button to `/bots`; admins SHALL also see a button to `/admin`.

#### Scenario: What the account screen shows
- **WHEN** the builder state is `none`, `requested` or `approved`, or the user is an admin
- **THEN** the screen shows the request button, the waiting note, the "My bots" button, or the "My bots" and "Admin" buttons

#### Scenario: No phone
- **WHEN** a user with no phone and builder state `none` opens the account screen
- **THEN** the screen shows the "verify your mobile first" note and no request button

#### Scenario: Not a builder opens My bots
- **GIVEN** a user who logged in by email, with no phone and builder state `none`
- **WHEN** the user opens "My bots"
- **THEN** the address is `/bots`, the title is "My bots", and the page says to verify the mobile first, with its button

### Requirement: CLI-24 My bots
`/bots` SHALL list the builder's bots, each a row with its icon (CLI-50), name, `@username`, online or last seen, and a suspended mark; a tap opens `/bots/<username>` (CLI-49). With no bots it SHALL show an empty state with a "Create a bot" button. The title row SHALL have "Create a bot", which opens `/bots/new`: username, name and an optional icon. After the create, the client SHALL open the new bot's page with its token shown (CLI-49). The chat list (`/chats`) SHALL have a "Create a bot" button for every user that opens `/bots`.

#### Scenario: List
- **GIVEN** a builder with bots `a_bot` (online) and `b_bot` (last seen yesterday)
- **WHEN** `/bots` is shown
- **THEN** there are two rows with icon or letter, name, username and status, and no token or guide on this page

#### Scenario: Empty
- **WHEN** a builder with no bots opens `/bots`
- **THEN** the page shows the empty state and "Create a bot"

#### Scenario: Create
- **WHEN** the builder creates `weather_bot` on `/bots/new` with an icon
- **THEN** the address becomes `/bots/weather_bot`, the token is shown once with Copy, and the bot has the icon

#### Scenario: Token shown once
- **WHEN** a builder creates a bot
- **THEN** the token is shown with the copy button on the bot's page, and after closing it or loading the page again the page shows the bot without the token

### Requirement: CLI-38 Guide to run a bot
A visitor and a logged-in user SHALL see at `/guide` a choice of plan: a bot or a site. In both languages, `/guide/bot` SHALL show nine steps to run a bot without an AI agent and `/guide/site` four steps (CLI-39), with addresses and numbers from the public settings. A bot's page (CLI-49) SHALL show steps 5 to 9 of the bot guide, numbered from 1, made for that bot. The token test SHALL show where the whole token goes; on the bot's page it SHALL hold the new token while that token is on screen, else `<bot id>:<TOKEN>`.

#### Scenario: Steps use the real addresses
- **GIVEN** the public settings say `bot_api_url` is `https://api.example.ir`, `site_url` is `https://example.ir` and the expiry is 60 seconds
- **WHEN** the guide is made in Persian and in English
- **THEN** both have nine steps; the token test is `curl https://api.example.ir/bot12:AbCdEfGhIjKlMnOp/getMe` and its text names `bot`; the run step shows `https://example.ir/@`; the last step says 60 seconds

#### Scenario: Every library points at Dalan
- **WHEN** the library code is made for `https://api.example.ir`
- **THEN** there are seven samples (python-telegram-bot, aiogram, pyTelegramBotAPI, Telegraf, grammY, node-telegram-bot-api, Telegram.Bot), each has `https://api.example.ir` and none has `api.telegram.org`

#### Scenario: Site guide
- **GIVEN** the public settings say `site_url` is `https://example.ir`
- **WHEN** the site guide is made in Persian and in English
- **THEN** both have four steps; the install step has `curl -fsSL https://example.ir/install.sh | sh` and `irm https://example.ir/install.ps1 | iex`; the last step has `dalan http 3000 --name mysite`

#### Scenario: Which screen
- **WHEN** the address is `/guide`, `/guide/bot` or `/guide/site`, for a visitor or a logged-in user
- **THEN** both get the plan choice, the bot guide and the site guide

#### Scenario: My bots fills in the new token
- **GIVEN** the page of bot 7 `weather_bot` shows the new token `7:XyZ`
- **WHEN** its run steps are made
- **THEN** they are numbered from 1, the first is the token test `curl https://api.example.ir/bot7:XyZ/getMe`, the run step shows `https://example.ir/@weather_bot`, and with no token shown the test is `curl https://api.example.ir/bot7:<TOKEN>/getMe`

## ADDED Requirements

### Requirement: CLI-46 Record a voice
While the input is empty, a 🎤 button SHALL take Send's place when the browser can record. A tap SHALL record and show the time and Cancel; the send button SHALL stop and send it as a voice (REL-28). The voice SHALL be OGG/Opus: recorded so, or WebM/Opus rewritten on the device, else the browser's own format with its real type. Cancel or leaving the chat SHALL stop the microphone and send nothing. At `max_file_size_mb` it SHALL stop and send.

#### Scenario: WebM rewritten
- **GIVEN** a WebM/Opus recording (saved test sample)
- **WHEN** it is rewritten
- **THEN** the result starts with `OggS`, has the `OpusHead` and `OpusTags` pages, has the same Opus packets in order, and its last granule position matches the length

#### Scenario: Type picked
- **WHEN** the browser supports `audio/ogg;codecs=opus`, or only `audio/webm;codecs=opus`, or only `audio/mp4`
- **THEN** the voice is sent as `audio/ogg`, as `audio/ogg` after the rewrite, or as `audio/mp4`

#### Scenario: Cancel
- **WHEN** the user cancels a recording
- **THEN** nothing is sent and the microphone is off

### Requirement: CLI-47 Paste or drop a file
Pasting (Ctrl+V) an image or a file into the input, or dropping a file on the chat, SHALL open the same card as 📎 (CLI-16): a preview of an image or the file name and size, a caption field holding the input text, Send and Cancel. A pasted image with no name SHALL be named `image.<type>`. Pasting only text SHALL paste text as before. Send SHALL go through REL-28, so the bot gets `message.photo` for an image and `message.document` for other files.

#### Scenario: Paste a screenshot
- **WHEN** the user pastes a PNG from the clipboard and sends it with the caption "bug"
- **THEN** the card showed the picture, and the bot gets a photo with caption "bug"

#### Scenario: Paste text
- **WHEN** the user pastes text
- **THEN** no card opens and the text is in the input

#### Scenario: Drop a PDF
- **WHEN** the user drops a PDF on the chat and sends it
- **THEN** the bot gets a document

### Requirement: CLI-48 Message text like Telegram
Message bubbles SHALL use the sizes of Telegram Desktop at the same window width: text 14px with line height 1.35 (Inter for Latin, Vazirmatn for Persian), bubble padding 6px at the top and bottom and 10px at the sides, the time 12px. They SHALL be the same in light and dark, and on phone and desktop widths. The exact values are in one place in `app.css` (`--message-*` tokens).

#### Scenario: Sizes
- **WHEN** a Persian and a Latin message are shown at 360 and at 1280 pixels wide
- **THEN** the text is 14px, the time 12px, in both themes

### Requirement: CLI-49 A bot's own page
`/bots/<username>` SHALL show one of the builder's bots: icon (CLI-50), name with rename, status, its link `<site_url>/@<username>` with Open and Copy, New token and Delete (both ask first), the showcase part (CLI-29), and the run guide (CLI-38) in three tabs: Telegram bot, CLI and MCP. Every value SHALL be this bot's. The token SHALL show only right after create or New token, until Close or leaving. Another username SHALL show "bot not found" with a link to `/bots`.

#### Scenario: Only this bot
- **GIVEN** the builder has bots 4 `mofidbot` and 9 `other_bot`, and just made a new token for `other_bot`
- **WHEN** the builder opens `/bots/mofidbot`
- **THEN** every link, id and command names `mofidbot` and 4, and no token is shown

#### Scenario: Link
- **WHEN** the builder taps Copy at the link
- **THEN** `https://dalanet.ir/@mofidbot` is copied and a toast says so

#### Scenario: Unknown bot
- **WHEN** the builder opens `/bots/nobody_bot`
- **THEN** the page says the bot was not found and links to `/bots`

### Requirement: CLI-50 Bot icons
The bot icon (REL-51) SHALL show in the chat header, the chat list, My bots, the bot's page and the showcase, round and cut to fill, at `?v=<icon_version>`; with no icon, or one that fails to load, the first letter. The picker on `/bots/new` and the bot's page SHALL take PNG, JPEG or WebP, cut the middle square to 512 by 512, and lower the JPEG quality until it fits `max_bot_icon_kb`.

#### Scenario: No icon
- **WHEN** a bot with `icon_version` 0 is in the chat list
- **THEN** its row shows the first letter and asks for no image

#### Scenario: Square cut
- **WHEN** the builder picks a 1200 by 800 picture
- **THEN** the upload is a 512 by 512 JPEG of the middle 800 by 800, at most `max_bot_icon_kb`

### Requirement: CLI-51 One address per screen
Each screen SHALL have its own address, and opening it (reload, Back, a shared link) SHALL show that screen: `/chats`, `/@<username>`, `/bots`, `/bots/new`, `/bots/<username>`, `/account`, `/admin`, `/apps`. The user menu SHALL go to these addresses. `/admin` for a non-admin SHALL go to `/chats`. After login the client SHALL open the address asked for, else `/chats`.

#### Scenario: Reload keeps the screen
- **GIVEN** the user is on the account screen
- **WHEN** the page is loaded again
- **THEN** the address is `/account` and the account screen is shown

#### Scenario: Different addresses
- **WHEN** the user opens My bots, then the account, then the chats from the menu
- **THEN** the addresses are `/bots`, `/account` and `/chats`, and Back goes to `/account`, then `/bots`

#### Scenario: Login lands where asked
- **GIVEN** a visitor opens `/bots/new`
- **WHEN** the visitor logs in
- **THEN** the address is `/bots/new`
