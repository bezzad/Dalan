# Client

## MODIFIED Requirements

### Requirement: CLI-09 Installable
The page SHALL link a web app manifest with a name, icons, `display` "standalone" and a start address on the same origin, and SHALL register a service worker from the same origin. On a bot's chat page the link SHALL point to that bot's manifest (REL-54, CLI-55).

#### Scenario: Manifest
- **WHEN** a browser asks for the manifest
- **THEN** it is JSON with a name, `display` "standalone", `start_url` "/" and at least one icon that the server serves

#### Scenario: Service worker
- **WHEN** a browser asks for `/sw.js`
- **THEN** the response is JavaScript with `Cache-Control: no-cache`

### Requirement: CLI-13 Inline buttons
The client SHALL show the inline keyboard of a message under that message, each row as sent. A tap on a button with `callback_data` SHALL send the press (REL-21) and show it in progress (CLI-53); a tap on a `url` button SHALL open the link in a new tab without the site as referrer, and the button SHALL have a small ↗ mark at its top end corner; a tap on a `switch_query` button SHALL put `@username`, a space and the query in the input and focus it, keeping nothing else. A button's `style` SHALL colour it in both themes: `success` green (`--brand`), `danger` red (`--danger`), `primary` blue (`--primary`), with readable text. An `answer` event with a text SHALL be shown: as an alert when `show_alert` is true, otherwise as a short note.

#### Scenario: Buttons of a message
- **GIVEN** message 2 has the inline buttons `[[A],[B]]`
- **WHEN** the chat is shown
- **THEN** message 2 has two rows of buttons under it

#### Scenario: Link mark and colour
- **GIVEN** a message has a `url` button and a `callback_data` button with style `danger`
- **WHEN** the chat is shown in light and in dark
- **THEN** the link button has the ↗ mark, and the other is red with text that passes the WCAG AA contrast check

#### Scenario: Fill button
- **GIVEN** a message of @mofidbot has a button with `switch_query` "todo 7"
- **WHEN** the user taps it
- **THEN** the input says "@mofidbot todo 7" and has the focus

## ADDED Requirements

### Requirement: CLI-52 Reply in the chat
The user SHALL start a reply by a message's menu (right-click or long press) → Reply, a double-click, or a swipe to the left. A bar above the input SHALL show the quote, with ✕ and Esc to cancel; the next message or file SHALL carry `reply_to` (REL-52). A message that replies SHALL show the quote on top: the writer ("You" or the bot's name) and the first line, or the file name. A click on a quote SHALL go to that message like CLI-14.

#### Scenario: Reply to an answer
- **GIVEN** bot message 2 "Which session?"
- **WHEN** the user picks Reply on it and sends "the second"
- **THEN** the send has `reply_to` 2, the bar is gone, and the new message shows a quote of "Which session?"

#### Scenario: Cancel
- **GIVEN** the reply bar is open
- **WHEN** the user presses ✕
- **THEN** the bar is gone and the next message has no `reply_to`

#### Scenario: Quote line
- **GIVEN** a message replies to a bot message "line one\nline two"
- **WHEN** the chat is shown
- **THEN** the quote shows the bot's name and "line one"

#### Scenario: Target deleted
- **GIVEN** a message replies to message 2, and message 2 was deleted
- **WHEN** the chat is shown
- **THEN** the quote says "Deleted message"

### Requirement: CLI-53 Press in progress
After a tap on a `callback_data` button, that button SHALL show a moving skeleton and every button of that message SHALL be locked, until an `answer` event for that press arrives, the message comes again with a higher version, the press fails, or `press_wait_seconds` (REL-18) pass. With reduced motion, the skeleton SHALL not move.

#### Scenario: Wait for the bot
- **GIVEN** message 2 has buttons A and B
- **WHEN** the user taps A
- **THEN** A is marked in progress and A and B do not take taps

#### Scenario: The bot answers
- **GIVEN** A is in progress for press "77"
- **WHEN** an `answer` event for press "77" arrives, or message 2 arrives with a higher version
- **THEN** A and B take taps again

#### Scenario: No answer
- **GIVEN** A is in progress and `press_wait_seconds` is 15
- **WHEN** 15 seconds pass with no answer
- **THEN** A and B take taps again

### Requirement: CLI-54 Formatted text
The client SHALL draw a message's entities (REL-53) with DOM elements and text nodes only, never by reading the text as HTML: `bold`, `italic`, `underline`, `strikethrough`, `code` and `pre` (monospace, `pre` as a block), `text_link` (opens in a new tab without referrer), `spoiler` (hidden until tapped), `blockquote` and `expandable_blockquote` (closed to three lines until tapped). Entities inside each other SHALL nest.

#### Scenario: Bold
- **GIVEN** "hi Ali" with a `bold` entity at 3 length 3
- **WHEN** the message is drawn
- **THEN** "Ali" is in a bold element and "hi " is plain text

#### Scenario: Markup stays text
- **GIVEN** a text `<img src=x onerror=alert(1)>` with a `bold` entity over it
- **WHEN** the message is drawn
- **THEN** the bold element holds that text as text, and there is no `img` element

#### Scenario: Nested
- **GIVEN** "abc" with `bold` 0..3 and `italic` 1..2
- **WHEN** the message is drawn
- **THEN** the italic "b" is inside the bold element

### Requirement: CLI-55 Install a bot
On a bot's chat page (`/@username`) the page SHALL link `/@username/manifest.webmanifest`; on every other screen it SHALL link the main manifest again. So the browser's Install on a bot's page installs that bot, and the installed app opens its chat.

#### Scenario: Bot page
- **WHEN** the chat of @mofidbot is shown
- **THEN** the manifest link is `/@mofidbot/manifest.webmanifest`

#### Scenario: Back to the list
- **WHEN** the user goes from the chat to `/chats`
- **THEN** the manifest link is `/manifest.webmanifest`
