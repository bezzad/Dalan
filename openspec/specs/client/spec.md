# client Specification

## Purpose
The web app people use: a small PWA in plain TypeScript, Persian first, served by the same process, that loads nothing from a foreign host.

## Requirements

### Requirement: CLI-01 The server serves the client
The server SHALL serve the PWA page at `/`, `/bots`, `/apps`, `/privacy`, `/faq`, `/showcase`, `/guide`, `/guide/bot`, `/guide/site` and `/@<bot username>`, and its files (scripts, styles, font, icons) from the same origin. The page SHALL be sent with `Cache-Control: no-cache`, so a new release is seen at the next open. Any other unknown path SHALL get 404.

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response is 200 HTML with `Cache-Control: no-cache`

#### Scenario: Open a bot link
- **WHEN** a browser asks for `/@ArcaneBot`
- **THEN** the response is the same page

#### Scenario: Open a public page
- **WHEN** a browser asks for `/privacy`, `/faq`, `/showcase`, `/guide`, `/guide/bot` or `/guide/site`
- **THEN** the response is the same page

#### Scenario: Unknown path
- **WHEN** a browser asks for `/nothing-here`
- **THEN** the response is 404

### Requirement: CLI-02 The client is inside the release
The built client SHALL be embedded in the server binary, so the release stays one executable and one settings file (DEL-03).

#### Scenario: Release holds the page
- **WHEN** the release is published
- **THEN** its folder holds only the executable and the settings file, and the page is inside the executable

### Requirement: CLI-03 Nothing from a foreign host
No built client file SHALL name a foreign host: no `http://` or `https://` address and no `//host` address in any HTML, CSS, JavaScript or manifest file.

#### Scenario: Scan the built files
- **WHEN** the built client files are scanned
- **THEN** no file holds an address of another host

### Requirement: CLI-04 Persian first, English second
The page SHALL start in Persian, right to left. The user SHALL be able to switch to English (left to right) and back, and the choice SHALL be remembered on that device. Every text SHALL exist in both languages.

#### Scenario: First open
- **WHEN** the page is opened for the first time
- **THEN** the language is Persian and the direction is right to left

#### Scenario: Switch language
- **WHEN** the user chooses English
- **THEN** the language is English, the direction is left to right, and the next open starts in English

#### Scenario: No missing text
- **WHEN** the Persian and English text lists are compared
- **THEN** both have the same keys and no text is empty

### Requirement: CLI-05 One ordered message list
The client SHALL keep the messages of a chat in rising `id` order with each `id` once, no matter whether a message arrived live, by sync, as the answer to a send, or more than once. Of two copies of one message the one with the higher `version` SHALL win. A message marked `deleted` SHALL leave the list. A change to a message older than the oldest one loaded SHALL NOT be added to the list.

#### Scenario: Same message twice
- **GIVEN** a list with messages 1 and 2
- **WHEN** message 2 arrives again and then message 3
- **THEN** the list is 1, 2, 3

#### Scenario: Out of order
- **WHEN** message 5 arrives before message 4
- **THEN** the list shows 4 before 5

#### Scenario: Edit
- **GIVEN** message 2 with version 2
- **WHEN** message 2 arrives with version 5 and a new text, and then the old copy with version 2 again
- **THEN** the list shows the new text

#### Scenario: Delete
- **WHEN** message 2 arrives with `deleted` true
- **THEN** message 2 is not in the list

#### Scenario: Change to an old message
- **GIVEN** the list starts at message 50
- **WHEN** an edit of message 3 arrives
- **THEN** the list still starts at message 50

### Requirement: CLI-06 Sync on open and on reconnect
When a chat is opened the client SHALL load the newest messages. Each time the event stream opens again the client SHALL ask for the messages after the highest `version` it has seen in that chat, and SHALL ask again while a full page (`sync_page_size`, REL-18) comes back. A live message that arrives during a sync SHALL NOT make the sync skip older changes.

#### Scenario: Open a chat
- **WHEN** a chat is opened with no messages on the device
- **THEN** the client asks for the newest messages (no `after`)

#### Scenario: Reconnect
- **GIVEN** the highest version on the device is 7
- **WHEN** the event stream opens again
- **THEN** the client asks for the messages after 7

#### Scenario: More than one page was missed
- **GIVEN** a sync answer is a full page
- **WHEN** it arrives
- **THEN** the client asks again after the highest `version` of that page, until a page is not full

#### Scenario: Live message during a sync
- **GIVEN** the highest version on the device is 7 and a sync is running
- **WHEN** a message with version 12 arrives live before the sync answer
- **THEN** the sync still asks after 7 and the changes 8 to 11 are not lost

#### Scenario: Live message for a chat that is not open
- **GIVEN** a chat that was never opened on this device
- **WHEN** a live message arrives for it and the chat is opened later
- **THEN** the client loads the newest messages of that chat

### Requirement: CLI-07 Reply buttons
The client SHALL show the newest bot reply keyboard under the input bar, like a phone keyboard; an empty one removes it. Each row SHALL keep its buttons on one line, sharing the width equally, never joined or split; `style` colours a button (CLI-13). A tap SHALL send the button's text. `resize` false: a fixed height the rows share; true: one button high per row. `one_time` hides it after a tap. Unless `persistent`, a ⌨ button hides and shows it; the next keyboard brings it back.

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

### Requirement: CLI-08 Reconnect with a growing wait
When the event stream ends the client SHALL open it again after a wait that starts at `reconnect_min_seconds`, doubles after each failed try, never passes `reconnect_max_seconds`, and goes back to the start after a stream opened. The values come from `GET /api/settings` (REL-18).

#### Scenario: Three failures
- **GIVEN** the minimum is 1 second and the maximum is 30
- **WHEN** the stream fails three times in a row
- **THEN** the waits are 1, 2 and 4 seconds

#### Scenario: Never above the maximum
- **WHEN** the stream keeps failing
- **THEN** no wait is longer than the maximum

#### Scenario: Success resets the wait
- **GIVEN** the wait has grown
- **WHEN** a stream opens
- **THEN** the next wait is the minimum again

### Requirement: CLI-09 Installable
The page SHALL link a web app manifest with a name, icons, `display` "standalone" and a start address on the same origin, and SHALL register a service worker from the same origin. On a bot's chat page the link SHALL point to that bot's manifest (REL-54, CLI-55).

#### Scenario: Manifest
- **WHEN** a browser asks for the manifest
- **THEN** it is JSON with a name, `display` "standalone", `start_url` "/" and at least one icon that the server serves

#### Scenario: Service worker
- **WHEN** a browser asks for `/sw.js`
- **THEN** the response is JavaScript with `Cache-Control: no-cache`

### Requirement: CLI-10 Proof of work in the browser
Before it asks for an SMS the client SHALL solve the server's challenge (SEC-06): find a `nonce` such that the SHA-256 hash of `<challenge>.<nonce>` starts with at least `difficulty` zero bits. It SHALL use the browser's own SHA-256 and no outside code.

#### Scenario: Solve
- **WHEN** the client solves a challenge with difficulty 8
- **THEN** the SHA-256 hash of `<challenge>.<nonce>` starts with a zero byte

#### Scenario: Same counting as the server
- **WHEN** the zero bits of the SHA-256 hash of `abc.0` are counted
- **THEN** the count is 2, as on the server

### Requirement: CLI-11 The session renews itself
When a call gets 401 the client SHALL call `POST /api/auth/refresh` once and then try the call again. Calls that get 401 at the same moment SHALL share one refresh. When the second try is 401 too, the client SHALL show the login screen. The refresh call itself and the login calls SHALL NOT be tried again.

#### Scenario: Access token ended
- **GIVEN** a call that gets 401
- **WHEN** the refresh succeeds
- **THEN** the call is sent again and its answer is used

#### Scenario: Two calls, one refresh
- **GIVEN** two calls that get 401 at the same moment
- **WHEN** they are handled
- **THEN** `POST /api/auth/refresh` is called once and both calls are sent again

#### Scenario: Session is over
- **GIVEN** a call that gets 401 and a refresh that gets 401
- **WHEN** the call is tried again and gets 401
- **THEN** the 401 reaches the caller

#### Scenario: Wrong password is not a reason to refresh
- **WHEN** `POST /api/auth/password` gets 401
- **THEN** no refresh is called

### Requirement: CLI-12 Persian digits work
The client SHALL turn Persian and Arabic digits into Latin digits in the phone number and the SMS code before it sends them, because Persian keyboards type them.

#### Scenario: Persian phone number
- **WHEN** the user types `۰۹۱۲۱۲۳۴۵۶۷`
- **THEN** the client sends `09121234567`

#### Scenario: Arabic digits
- **WHEN** the user types the code `١٢٣٤٥٦`
- **THEN** the client sends `123456`

### Requirement: CLI-13 Inline buttons
The client SHALL show a message's inline keyboard under it, rows as sent. A `callback_data` tap SHALL send the press (REL-21), shown in progress (CLI-53); a `url` tap SHALL open the link in a new tab with no referrer, marked ↗ at its top end; a `switch_query` tap SHALL put only `@username`, a space and the query in the input and focus it. `style`: `success` `--brand`, `danger` `--danger`, `primary` `--primary`, in both themes. An `answer` text shows as an alert with `show_alert`, else a note.

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

### Requirement: CLI-14 Pinned message bar
The client SHALL show the pinned message of the chat (REL-25) in a bar above the messages, taken from the `pinned` field of the newest messages answer and kept up to date by changes: a message with `pinned` true becomes the bar; the bar's message without `pinned`, or deleted, clears it. A click on the bar SHALL close the search, load older pages until the message is there, scroll to it and mark it for a moment; a message that is not in the history gives a short note.

#### Scenario: Pin moves
- **GIVEN** the bar shows message 2
- **WHEN** message 2 arrives without `pinned` and then message 3 with `pinned` true
- **THEN** the bar shows message 3

#### Scenario: Unpin
- **GIVEN** the bar shows message 2
- **WHEN** message 2 arrives without `pinned`
- **THEN** there is no bar

#### Scenario: Jump to an older pinned message
- **GIVEN** the pinned message 2 is older than the loaded page
- **WHEN** the bar is clicked
- **THEN** older pages load until message 2 is there, and the chat scrolls to it

#### Scenario: Pinned message not in the history
- **GIVEN** the pinned message is older than every message the server keeps
- **WHEN** the bar is clicked
- **THEN** older pages load to the start, and a note says it is not in the history

### Requirement: CLI-15 Command menu
When the bot has commands (REL-26), the client SHALL show a menu button by the input. Choosing a command SHALL send `/<command>` as a message.

#### Scenario: Menu
- **GIVEN** the bot set the commands "start" and "help"
- **WHEN** the user opens the menu and picks "help"
- **THEN** the message "/help" is sent

### Requirement: CLI-16 Files in the chat
The client SHALL let the user pick, paste or drop a file (CLI-47), show it in a card, and send it with the card's text as its caption (REL-28); a file over `max_file_size_mb` (REL-18) SHALL be refused before sending. A `file` SHALL show as: a photo or picture document, an image (CLI-89); HTML, a card that opens in the app (CLI-90); voice or audio, the Dalan player; video, a video player; anything else (PDF too), a link with its name and size.

#### Scenario: Kind of view
- **WHEN** messages with a photo, a PNG document, an HTML document, a voice, a video and a PDF document are shown
- **THEN** they use an image, an image, an HTML card, the Dalan player, a video player and a link

#### Scenario: Voice length
- **GIVEN** a voice with `duration` 65
- **WHEN** it is shown and not playing
- **THEN** it says 1:05

#### Scenario: Too big
- **WHEN** the user picks a file over the limit
- **THEN** nothing is sent and a message says the file is too big

### Requirement: CLI-17 Edited messages are marked
A message with `edited` SHALL show a small "edited" mark beside its time.

#### Scenario: Edited
- **WHEN** a message with `edited` is shown
- **THEN** it has the "edited" mark

### Requirement: CLI-18 Unread counts
The bot list SHALL show each bot's `unread` count (REL-14) when it is above 0. While a chat is open and the page is visible, the client SHALL send the read position (REL-32) up to the newest message it shows, only when that is above the last one it sent. While the list is shown, a `message` or `read` event SHALL make the client load the list again, at most once a second.

#### Scenario: Next read position
- **GIVEN** the client sent read position 4
- **WHEN** the newest shown message is 4, and later 6
- **THEN** nothing is sent the first time, and 6 the second time

#### Scenario: Hidden page
- **WHEN** a message arrives while the page is hidden
- **THEN** no read position is sent until the page is visible again

### Requirement: CLI-19 Bot status line
The chat header SHALL show "online" when the bot is online, else "last seen" with the time in words ("just now", "N minutes ago", "N hours ago", or the date), else nothing for a bot never seen. The client SHALL ask again every `status_refresh_seconds` (REL-18) while the chat is open.

#### Scenario: Last seen
- **GIVEN** a bot offline whose `last_seen` is 5 minutes ago
- **WHEN** the status is drawn in English
- **THEN** it reads "last seen 5 minutes ago"

#### Scenario: Persian
- **WHEN** the same status is drawn in Persian
- **THEN** it uses Persian words and digits

### Requirement: CLI-20 "Not delivered" mark
A user message with `not_delivered` SHALL show a "not delivered" mark next to its time.

#### Scenario: Mark
- **WHEN** a user message with `not_delivered` true is drawn
- **THEN** its time line ends with the "not delivered" mark

### Requirement: CLI-21 Notifications switch
The account screen SHALL have a notifications switch, shown only when the server has a push key (REL-35). Turning it on SHALL ask the browser's permission, subscribe with the server's key and send the subscription; turning it off SHALL unsubscribe on the server and in the browser. When the browser cannot push (for example an iPhone before "Add to Home Screen") or the permission was denied, the screen SHALL say so instead. Logout SHALL first remove this device's subscription.

#### Scenario: What the switch shows
- **WHEN** the server has no key, the browser cannot push, the permission is denied, or a subscription exists
- **THEN** the switch is hidden, a "cannot" note shows, a "blocked" note shows, or the switch is on

### Requirement: CLI-22 The service worker shows pushes
The service worker SHALL show each push as a notification with its title, body and tag, except when a focused window already shows that chat. A tap SHALL open or focus the chat's address.

#### Scenario: Push while the chat is in front
- **GIVEN** a focused window shows `/@shopbot`
- **WHEN** a push for `/@shopbot` arrives
- **THEN** no notification is shown

#### Scenario: Push while away
- **GIVEN** no window is focused
- **WHEN** a push arrives
- **THEN** a notification with its title, body and tag is shown

#### Scenario: Tap
- **WHEN** the user taps a notification for `/@shopbot`
- **THEN** an open window goes to `/@shopbot` and gets focus, or a new one opens there

### Requirement: CLI-23 Builder request in the account screen
A user with a verified phone SHALL be a builder with no request (AUTH-39): no "become a builder" button and no "waiting for approval". A user with no phone SHALL see, on the Builder section and on My bots, a note that building needs a verified mobile number, with a button to verify it (CLI-34). A user the admin refused SHALL see that note. For a builder the dashboard SHALL show My bots and My apps.

#### Scenario: What the account screen shows
- **GIVEN** a user with no phone
- **WHEN** the Builder section opens
- **THEN** it says building needs a verified mobile number and has a button to verify it, and no request button

#### Scenario: A verified phone
- **WHEN** a user with a verified phone opens the Builder section
- **THEN** it says they can build, with a link to My bots, and no request button

#### Scenario: No phone
- **WHEN** a user with no phone and builder state `none` opens the account screen
- **THEN** the screen shows the "verify your mobile first" note and no request button

#### Scenario: Not a builder opens My bots
- **GIVEN** a user who logged in by email, with no phone and builder state `none`
- **WHEN** the user opens "My bots"
- **THEN** the address is `/bots`, the title is "My bots", and the page says to verify the mobile first, with its button

### Requirement: CLI-24 My bots
`/bots` SHALL list the builder's bots: icon (CLI-50), name, `@username`, online or last seen, a suspended mark; a tap opens `/bots/<username>` (CLI-49). With none, an empty state with "Create a bot". The title row SHALL have "Create a bot", opening `/bots/new` (username, name, optional icon); after it the client SHALL open the new bot's page with its token shown (CLI-49). The chat list SHALL have a "Create a bot" button for every user that opens `/bots`.

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

### Requirement: CLI-25 Admin screen
The admin screen SHALL have tabs: settings (grouped by section, each with its value, an edit field, a reset button when changed, and an "after restart" mark), users (search; approve or refuse a builder request; suspend or unsuspend; open the user's access log), builder requests, bots (search; suspend or unsuspend; open the bot's access log), access log (search by user, bot, phone or address; times shown in UTC and local time) and audit log. A value is checked by its kind before it is sent.

#### Scenario: Settings grouped and checked
- **GIVEN** settings `Relay:MaxTextLength` (number) and `Admin:PageSize` (number)
- **WHEN** the settings tab is drawn and the admin types `abc` for a number
- **THEN** they are in the groups `Admin` and `Relay`, and the save button is off for `abc`

#### Scenario: Suspended bot in a chat
- **WHEN** a user writes to a suspended bot
- **THEN** the chat shows that the bot is suspended

### Requirement: CLI-26 Landing page
A visitor and a logged-in user SHALL see at `/` a landing page in the current theme (CLI-35) that says only what Dalan does, its features and its plans, in this order: a hero with "Try it (free)" and "See plans"; the plans (`#plans`), product cards with a traffic picture, each with "Get started" to its guide (CLI-38); what you get (CLI-37); the three messages of DESIGN section 13; a security list; a closing call; a footer. It SHALL NOT show install steps or code, or say "tunnel".

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by email or SMS without building a login; keeps working when outside links are down (Persian: when international internet is cut). None says "tunnel"

#### Scenario: Diagram parts
- **WHEN** the bot card picture is read in Persian or English
- **THEN** it goes from the users through Dalan to your computer, and the link to the computer says the call goes out from the computer only

#### Scenario: Each plan opens its guide
- **WHEN** the plans are read in Persian or English
- **THEN** there are two, bot relay and sites, both built, and their guides are `/guide/bot` and `/guide/site`

#### Scenario: No install steps
- **WHEN** the landing page is drawn
- **THEN** it has no code block, no command tabs and no steps

#### Scenario: Which screen
- **WHEN** the address is `/`, `/bots`, `/privacy`, `/faq`, `/showcase` or `/@bot`, for a visitor and for a logged-in user
- **THEN** a visitor gets landing, login, privacy, FAQ, showcase and login; a logged-in user gets landing, the bot list, privacy, FAQ, showcase and the chat

### Requirement: CLI-27 Privacy page and FAQ
The privacy page SHALL say plainly, in both languages, the six points of DESIGN section 13 (showcase only on request, no phone number to bots, no end-to-end encryption, history kept, the home machine is the builder's, the access log). The numbers SHALL come from ADM-14, never from the text. The FAQ SHALL answer the same six points as questions.

#### Scenario: Numbers from the server
- **GIVEN** the public settings say 200 messages, 30 days and 180 days of access log
- **WHEN** the privacy page and the FAQ are drawn in Persian and in English
- **THEN** each has six points, and the numbers 200, 30 and 180 are in them (in the language's digits)

### Requirement: CLI-28 Showcase page
The showcase page SHALL show the listed bots (icon or first letter, name, `@username`, description, category), a filter by category and a "more" button while a full page came back. "Open" SHALL go to `/@username`; a visitor logs in first and then lands in that chat.

#### Scenario: Category names
- **WHEN** the categories `games` and `robots` are drawn
- **THEN** `games` has its Persian or English name and `robots` is shown as it is

### Requirement: CLI-29 Showcase form and review
In "My bots" each bot SHALL have a showcase part: its state (not listed, waiting for review, listed, refused), a form for description and category, an icon picker, and a "leave the showcase" button. It SHALL say that every change waits for review. The admin screen SHALL have a "showcase" tab with the pending entries (icon, name, username, description, category) and approve and refuse buttons.

#### Scenario: State names
- **WHEN** a bot with no entry, a pending, a listed and a refused entry are drawn
- **THEN** they say "not listed", "waiting for review", "listed" and "refused"

### Requirement: CLI-30 Access keys and ready prompt
For an approved builder or an admin, the account SHALL have a part for AI assistants (`#keys`): the keys (name, made, expires) with delete, a form that makes a key and shows it once with Copy, and a ready prompt to copy. "My bots" and "My apps" SHALL only link to it ("Access keys for AI assistants"). The prompt SHALL name the site and Bot API addresses from the public settings (ADM-15), `/llms.txt`, the MCP door and the CLI, and SHALL never hold a key.

#### Scenario: Ready prompt
- **GIVEN** the public settings say site `https://dalanet.ir` and Bot API `https://api.dalanet.ir`
- **WHEN** the ready prompt is made in Persian and in English
- **THEN** it has `https://dalanet.ir/llms.txt`, `https://dalanet.ir/api/mcp`, `https://dalanet.ir/dalan.mjs` and `https://api.dalanet.ir`

#### Scenario: In the account
- **GIVEN** an approved builder
- **WHEN** the account screen opens
- **THEN** it shows the AI assistants part with the keys, the form and the ready prompt

#### Scenario: Not a builder
- **GIVEN** a user who is not an approved builder or an admin
- **WHEN** the account screen opens
- **THEN** there is no AI assistants part

#### Scenario: My bots
- **WHEN** an approved builder opens "My bots"
- **THEN** it shows the bots and one link "Access keys for AI assistants" to `/account#keys`, and no keys, form or prompt

### Requirement: CLI-31 Command line tool
`node dalan.mjs <command>` SHALL read `DALAN_URL` and `DALAN_KEY` and call the builder REST calls with the key: `bots`, `create <username> <name>`, `token <bot id>`, `status <bot id>`. Success: the JSON answer on stdout, exit 0. An error answer: `{"error", "message"}` on stderr, exit 1. No or a wrong command, or a missing variable: the usage, exit 2.

#### Scenario: Create
- **WHEN** `node dalan.mjs create weather_bot Weather` runs against a server
- **THEN** it sends `POST /api/builder/bots` with `Authorization: Bearer <DALAN_KEY>` and the JSON body, prints the answer and exits 0

#### Scenario: Error
- **WHEN** the server answers 409 `username_taken`
- **THEN** stderr has the error code and message and the exit code is 1

#### Scenario: Usage
- **WHEN** it runs with no command, or without `DALAN_KEY`
- **THEN** it prints the usage and exits 2

### Requirement: CLI-32 The site serves the CLI
The site SHALL serve `/dalan.mjs`, one Node file (Node 18 or newer) that needs no package.

#### Scenario: Served by the site
- **WHEN** a browser or `curl` asks for `/dalan.mjs`
- **THEN** the answer is 200 and is the same file as `client/public/dalan.mjs`

### Requirement: CLI-33 Email login screen
When `email_login` is on (AUTH-16), the first login card (CLI-36) SHALL ask for the email address; then the proof of work (CLI-10), then the code and, for a new address, the name. After the code is sent the screen SHALL say that the code was sent and that the person should check the spam folder too. Errors `bad_email`, `email_wait`, `email_limit` and `email_unavailable` SHALL have their own texts in both languages.

#### Scenario: Spam note
- **WHEN** the email code was sent
- **THEN** the screen shows the address and the note to check the spam folder

#### Scenario: Texts
- **WHEN** the client gets `bad_email`, `email_wait`, `email_limit`, `email_unavailable`, `phone_required`, `phone_taken` or `has_phone`
- **THEN** it shows its own text, in Persian and in English

### Requirement: CLI-34 Verify a mobile number in the account screen
A user with no phone (AUTH-04 `has_phone` false) SHALL see a card in the account screen to add a mobile number: the number, the proof of work, then the SMS code (AUTH-26). After success the card SHALL go away and the builder request button SHALL show.

#### Scenario: Level 1 user
- **WHEN** a user with `has_phone` false opens the account screen
- **THEN** the "verify your mobile" card is shown; with `has_phone` true it is not

### Requirement: CLI-35 Light and dark theme
The client SHALL have a light and a dark theme, the device's theme on the first open. The fixed header of every page SHALL have, in the same place, a round theme button (light or dark only) beside a round language button of the same size, a globe that switches Persian and English in one click (CLI-04). The choice SHALL be kept on this device over the device's theme. The new theme SHALL grow as a circle from the button (at once with reduced motion). Code blocks SHALL stay dark in both themes.

#### Scenario: First open follows the device
- **WHEN** a visitor opens the site for the first time on a device that asks for dark
- **THEN** the theme is dark (`data-theme="dark"`), and nothing is kept yet

#### Scenario: Choice is kept
- **WHEN** the visitor presses the theme button twice, starting from light
- **THEN** the theme is dark, then light; there is no third state, and the next open uses the kept choice

#### Scenario: Language button
- **WHEN** the header tools are made in Persian, then in English
- **THEN** the language button is round and shows only the globe (no letters); its name for screen readers says the other language; one click switches the language

#### Scenario: Same size as the theme button
- **WHEN** a page is open in a browser at desktop and at phone width
- **THEN** the language and theme buttons are both 36 by 36 pixels, side by side in the header

### Requirement: CLI-36 One simple login card
The login screen SHALL be one card that opens with the password form (CLI-62). Under it SHALL come one equal button per other way: SMS, GitHub (`/api/auth/github`, when `github_login` is on) and email (when `email_login` is on), new browsers first SMS, returning ones (`dalan.returning`, set after any login) first GitHub. SMS and email SHALL open their form with a "back" button. The development login SHALL be a small link. A `login_error` (AUTH-29) SHALL show and leave the address.

#### Scenario: New browser
- **GIVEN** `github_login` and `email_login` are true and local storage has no `dalan.returning`
- **WHEN** the login screen opens
- **THEN** the buttons under the password form are, in order, SMS, GitHub, email

#### Scenario: Returning browser
- **GIVEN** the person logged in before on this browser
- **WHEN** the login screen opens
- **THEN** the buttons under the password form are, in order, GitHub, email, SMS

#### Scenario: GitHub on
- **GIVEN** `github_login` is true
- **WHEN** the login screen opens
- **THEN** a "Log in with GitHub" button links to `/api/auth/github`

#### Scenario: GitHub off
- **GIVEN** `github_login` is false
- **WHEN** the login screen opens
- **THEN** there is no GitHub button

#### Scenario: Error from GitHub
- **WHEN** the page opens at `/?login_error=github_unavailable`
- **THEN** the login card shows "GitHub cannot be reached now. Log in by email or SMS." in the person's language and the address becomes `/`

#### Scenario: Texts
- **WHEN** the error is `github_state`, `github_denied`, `github_no_email`, `github_unavailable` or `suspended`
- **THEN** it has its own text, in Persian and in English

### Requirement: CLI-37 What you get
The landing page SHALL have a "What you get" section after the plans: four groups in order (bot relay, users and safety, tools for builders, sites and gateway), each with a title, a short text and a list. The first three SHALL list only built things. In the fourth, the first item (a local site at a fixed Dalan subdomain, CLI-39) is built; every other item SHALL be marked "soon". One line SHALL say what Dalan does not offer. No text SHALL say "tunnel".

#### Scenario: Ready and soon
- **WHEN** the groups are read in Persian or English
- **THEN** there are four, no thing in the first three is marked soon, the first thing of the fourth is not marked soon, and every other thing in the fourth is marked soon

#### Scenario: Honest about what is not offered
- **WHEN** the not-offered line is read in Persian or English
- **THEN** it names TCP, SSH and the AI gateway, and no text of the section says "tunnel"

### Requirement: CLI-38 Guide to run a bot
Anyone SHALL see at `/guide` a choice of plan: a bot or a site. In both languages, `/guide/bot` SHALL show nine steps to run a bot without an AI agent and `/guide/site` five steps (CLI-39), with addresses and numbers from the public settings. A bot's page (CLI-49) SHALL show steps 5 to 9 of the bot guide, numbered from 1, made for that bot. The token test SHALL show where the whole token goes; on the bot's page it SHALL hold the new token while that token is on screen, else `<bot id>:<TOKEN>`.

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
- **THEN** both have five steps; the install step has `curl -fsSL https://example.ir/install.sh | sh` and `irm https://example.ir/install.ps1 | iex`; the try step has `dalan http 3000 --name mysite` and says Ctrl+C stops it; the last step has `dalan service install`

#### Scenario: Which screen
- **WHEN** the address is `/guide`, `/guide/bot` or `/guide/site`, for a visitor or a logged-in user
- **THEN** both get the plan choice, the bot guide and the site guide

#### Scenario: The bot's page fills in the new token
- **GIVEN** the bot's page shows the new token `7:XyZ`
- **WHEN** its run steps are made
- **THEN** they are numbered from 1, the first is the token test `curl https://api.example.ir/bot7:XyZ/getMe`, and with no token shown it is the sample again

### Requirement: CLI-40 Closed gate for an unknown address
An address the site does not know SHALL show a "not found" page instead of the landing page: the site header, the Dalan gate drawn closed, one short sentence and a button to the home page.

#### Scenario: Unknown address
- **WHEN** a visitor or a logged-in user opens `/no-such-page`
- **THEN** the page is "not found", with the closed gate and a button to `/`

### Requirement: CLI-41 English text never names Iran
No English text that the product shows SHALL name Iran: client texts, page titles, meta tags, the manifest, `/llms.txt`, and the error texts of the API. Persian text MAY name ایران. Where the English text spoke of "international internet is cut", it SHALL say it neutrally, for example "Keeps working when outside links are down" or "inside the country".

#### Scenario: Client English texts
- **WHEN** every English text of the client (texts, landing, menus, guides, security list) is read
- **THEN** none contains "Iran" in any case

#### Scenario: Built files
- **WHEN** the built client files (HTML, scripts, manifest) are scanned
- **THEN** none contains "Iran" in any case; Persian "ایران" is allowed

#### Scenario: Server texts
- **WHEN** a visitor reads `/llms.txt`, or sends a bad phone number to `/api/auth/sms`
- **THEN** the text does not contain "Iran" in any case

### Requirement: CLI-39 My apps
"My apps" (`/apps`) SHALL list a builder's sites (name, address link, online or offline), delete asks first, refreshed every 10 seconds, and a guide: 1) install the agent; 2) log in with a key from one button (the full `dalan login` command, once, Copy); 3) port and name: "Reserve" claims it or says it is taken; 4) `dalan http <port> --name <name>` (Ctrl+C stops it); 5) `dalan service install`, `dalan status`. Each action ends in a toast; a non-builder sees how to become one.

#### Scenario: Guide commands
- **WHEN** the guide is drawn for the name `mysite` and port `3000`
- **THEN** it shows `dalan http 3000 --name mysite` and `dalan service install`

#### Scenario: Reserve
- **WHEN** a builder reserves the name in the guide
- **THEN** the name appears in the list, and a taken name shows its error

#### Scenario: Not a builder
- **WHEN** a user who is not a builder opens "My apps"
- **THEN** they see the way to become a builder, and no site list

### Requirement: CLI-42 Simple header and a phone menu
The site header SHALL link only to Plans, Showcase and Guide; FAQ and Privacy SHALL be only in the footer. Below 760px the links SHALL fold into a round ☰ button after the header tools, which opens a panel with the same links; a link, a click outside or Escape SHALL close it. A tick icon in a list SHALL sit in the middle of the first text line. No page SHALL scroll sideways on a phone, in portrait or in landscape.

#### Scenario: Header and footer links
- **WHEN** a public page is drawn in Persian or English
- **THEN** the header links go to `/#plans`, `/showcase` and `/guide`, none goes to `/faq` or `/privacy`, and the footer links go to both

#### Scenario: Phone menu
- **WHEN** the header is drawn and the ☰ button is pressed, then pressed again
- **THEN** the panel opens with the three links and `aria-expanded` is `true`, then it closes and `aria-expanded` is `false`

#### Scenario: Folded on a phone
- **WHEN** the style sheet is read
- **THEN** below 760px the header links are hidden and the ☰ button is shown; above it the ☰ button is hidden

#### Scenario: List icons line up
- **WHEN** a list with tick icons (what you get, security, the guards of a plan) is drawn in Persian or English, at any width
- **THEN** each icon sits in the middle of the first line of its text, by the line height of the font, never by a fixed offset

#### Scenario: No sideways scroll
- **WHEN** the landing, guide, showcase, FAQ, privacy, login, bot list and chat pages are open in a browser at 360 by 740 and at 740 by 360 pixels
- **THEN** no page is wider than the screen

### Requirement: CLI-43 Natural, formal Persian
Every Persian text the client shows SHALL be a little formal and use the common Persian word of the glossary in docs/UI.md, never a literal translation. Dalan has bots, not robots: «بات», never «ربات». «می‌» and «ها» SHALL be joined with a zero-width non-joiner.

#### Scenario: No literal words
- **WHEN** every Persian text of the client (texts, landing, privacy, FAQ, both guides, the ready prompt) is read
- **THEN** none has «ربات», «رله», «اپ» as a word, «ایجنت», «ادمین», «پلن», «لاگ», «تم:», «گواه کار», «محدودیت سرعت», «سوال‌های رایج» or «گفتگو»

#### Scenario: Joined prefixes
- **WHEN** every Persian text of the client is read
- **THEN** no «می» stands alone before a space

### Requirement: CLI-44 Titles without a full stop
A title, heading, label or button text SHALL NOT end with a full stop, in Persian or English. Sentences in paragraphs, notes and toasts keep theirs.

#### Scenario: No full stop at the end of a title
- **WHEN** every title of the landing (messages, products, offers, hero, closing, labels), of both guides and every title text of the client is read in Persian and in English
- **THEN** none ends with «.»

### Requirement: CLI-45 The chat page is the bot's own page
The chat page SHALL have one header, in this order: the bot's icon (its showcase icon, else the first letter of its name), its name with a line of `@username` and its status (CLI-19), a search button, the theme button, the user menu shown as a round initial, and the Dalan mark that goes to `/`. It SHALL NOT have a Back button, the language button or the Dalan name. Search SHALL show only the loaded messages that match it (scenario Search).

#### Scenario: Header parts
- **WHEN** the chat header of `@mofid`, named `Arcane`, is drawn
- **THEN** it shows the letter `A`, `Arcane`, `@mofid`, then search, theme, the user menu and a link to `/`, and no Back or language button

#### Scenario: Search
- **GIVEN** messages "Hello", "the clock" and a file `Clock.png`
- **WHEN** the search has `clock`
- **THEN** only "the clock" and `Clock.png` are shown (text or file name, any case); with an empty search, Escape or ✕, all three are shown

### Requirement: CLI-58 Each line takes its own direction
In a message's text, each line SHALL take its direction from its own first strong letter: a Persian, Arabic or Hebrew letter makes it right to left, a Latin letter left to right. Each line SHALL line up at its own start (right for a right-to-left line). Lines inside a blockquote SHALL do the same. A code block (`pre`) SHALL stay left to right. The client SHALL NOT guess the language; it uses the Unicode bidi rule.

#### Scenario: Latin first line, then Persian
- **WHEN** a bot sends `🧵 Dalan [a37dcb87] · sms-ir` on the first line and `نیازی به تشخیص زبان نیست.` on the next
- **THEN** the first line is left to right at the left, and the second is right to left at the right, with the dot at its left end

#### Scenario: Persian first line, then Latin
- **WHEN** a message has `برنامه:` on the first line and `git push origin main` on the next
- **THEN** the first line is right to left and the second left to right

#### Scenario: Code stays left to right
- **WHEN** a message has a code block with Persian text in it
- **THEN** the code block is left to right

### Requirement: CLI-59 The phone reads the SMS code by itself
The SMS code field SHALL have `autocomplete="one-time-code"`. When the browser has WebOTP (`OTPCredential`), the SMS code screen SHALL ask for the code with `navigator.credentials.get({ otp: { transport: ["sms"] } })`, put the code in the field and send the form. The request SHALL be aborted when the form is sent or the screen is left. The login SMS SHALL end with the line `@<site domain> #<code>`.

#### Scenario: Android reads the code
- **GIVEN** a browser with WebOTP
- **WHEN** the SMS code screen opens and the browser gives the code `123456`
- **THEN** the field holds `123456` and the form is sent

#### Scenario: Browser without WebOTP
- **GIVEN** a browser without `OTPCredential`
- **WHEN** the SMS code screen opens
- **THEN** no credential request is made, and the user types the code

#### Scenario: Refused or left
- **WHEN** the browser refuses the request, or the user leaves the screen first
- **THEN** nothing is filled and no error is shown

#### Scenario: iOS
- **GIVEN** an iPhone and an SMS that ends with `@dalanet.ir #123456`
- **WHEN** the user taps the code field
- **THEN** iOS offers `123456` over the keyboard

### Requirement: CLI-60 Any usual phone format works
Before it sends a phone number (SMS login, password login, development login, verify your mobile), the client SHALL turn an Iranian mobile written in any usual way into `09XXXXXXXXX`: with `+98`, `0098`, `98` or no `0` at the start; with spaces, dashes, dots or brackets; with Persian or Arabic digits (CLI-12); with invisible direction marks. Other text SHALL only get Latin digits, so the server's answer stays the same.

#### Scenario: Copied from contacts
- **WHEN** the user types `+98 914 914 9202`, `0098-914-914-9202`, `(0914) 914 9202` or `9149149202`
- **THEN** the client sends `09149149202`

#### Scenario: Not an Iranian mobile
- **WHEN** the user types `+1 555 123 4567` as a phone
- **THEN** the client sends it as typed, and the server refuses it as before

### Requirement: CLI-61 Send the code again
The code screen SHALL have a "Send again" button, off with a countdown (`m:ss`) for `retry_in` seconds of the sent code, then on. Pressing it SHALL ask a new code for the same phone or email (with a new proof, CLI-10) and start the countdown again. A 429 with `Retry-After` on asking a code SHALL show "Try again in m:ss" with the seconds of the header. No text SHALL send the person to "tomorrow".

#### Scenario: Countdown
- **GIVEN** a code was sent with `retry_in` 120
- **WHEN** the code screen opens
- **THEN** "Send again" is off and shows `2:00`, and it is on after 120 seconds

#### Scenario: Too soon
- **WHEN** asking a code gets 429 `sms_wait` with `Retry-After: 1680`
- **THEN** the screen shows "Try again in 28:00"

### Requirement: CLI-70 Link and merge in the account screen
The account screen SHALL have a card to add a phone (CLI-34), add an email and link GitHub, each only while that way is empty. On `merge_needed` or `/account?merge=1` it SHALL show the merge question from `GET /api/account/merge`, with "Merge" and "Cancel". A refusal SHALL show its own text and no "Merge" button. All texts SHALL exist in Persian and in English.

#### Scenario: The question
- **GIVEN** an email user posts a phone of another account with the right code
- **WHEN** the answer is 409 `merge_needed`
- **THEN** the screen says "This phone already has an account. Merge it into this account?", which account is kept, the bots, sites and chats of each side, and that bots that chatted with the other account get its new chat id

#### Scenario: Cancel
- **WHEN** the person presses "Cancel"
- **THEN** the client sends `DELETE /api/account/merge` and the question closes

#### Scenario: Refused
- **GIVEN** the pending merge is refused with `both_have_products` or `merge_conflict`
- **WHEN** the question shows
- **THEN** it explains why and has no "Merge" button

### Requirement: CLI-62 Logo and password first
The login screen SHALL show the Dalan gate (`client/src/logo.ts`) above the card. The password form (login name, password, "Log in") SHALL be open at once, with no button to press first. A line "or" SHALL separate it from the other ways (CLI-36), and each other way SHALL have its own logo: a phone for SMS, an envelope for email, the GitHub mark for GitHub. `docs/UI.md` SHALL say so.

#### Scenario: Password form is first
- **WHEN** the login screen opens
- **THEN** it has the gate above the card, the password form inside it, and the other ways under the form

#### Scenario: Each way has a logo
- **WHEN** the buttons of the other ways are drawn
- **THEN** each has an icon before its words

### Requirement: CLI-63 Back to where the person came from
After a login the client SHALL show the address the person came from (`next`), taken from `?next=` or from `sessionStorage` key `dalan.next`, which the client SHALL keep before it leaves for GitHub and clear after use. Only an internal path SHALL be accepted. With no accepted address, a login from `/` SHALL go to "My bots" (`/bots`), and a login from any other address SHALL stay on it.

#### Scenario: Internal path
- **GIVEN** `next` is `/guide/site`
- **WHEN** the login ends
- **THEN** the screen is `/guide/site`

#### Scenario: Foreign address
- **GIVEN** `next` is `//evil.example` or `https://evil.example` or `/\evil.example`
- **WHEN** the login ends
- **THEN** the address is ignored and the person goes to `/bots`

#### Scenario: What an internal path is
- **GIVEN** `next` does not start with exactly one `/`, or starts with `//` or `/\`, or has a `\` or a control character, or is longer than 200 characters, or is `javascript:x`
- **WHEN** the login ends
- **THEN** the address is ignored

#### Scenario: GitHub round trip
- **GIVEN** the person pressed "Log in with GitHub" on `/guide/site`
- **WHEN** GitHub sends them back to `/`
- **THEN** after the login the screen is `/guide/site` and `dalan.next` is empty

#### Scenario: No address
- **GIVEN** the person logs in from `/` with no `next`
- **WHEN** the login ends
- **THEN** the screen is `/bots`

### Requirement: CLI-64 The guides are steppers
`/guide/bot` and `/guide/site` SHALL show their steps as a stepper: the step to do now is open with "Back" and "Next"; a step already done is green, says "Done" and is closed, with a button to open it. Which steps are done SHALL come from the server. The open step SHALL be the first step not done. A step the server cannot know SHALL be done only by "Next". A new and an old user SHALL see the same steps, in the same order.

#### Scenario: First not done
- **GIVEN** the user is logged in and a builder, has no key and no site online
- **WHEN** the site guide opens
- **THEN** step 1 is done, the install step is open, and the key step is not done

#### Scenario: Old user
- **GIVEN** the user has a key and a site that is online
- **WHEN** the site guide opens
- **THEN** the steps of login, key and try it are done and the open step is the last one

#### Scenario: Where the state comes from
- **GIVEN** a guide opens
- **WHEN** it decides which steps are done
- **THEN** logged in comes from `GET /api/me`, builder from its `builder` field, a key from `GET /api/account/keys` not empty, and a site online from `GET /api/sites`

### Requirement: CLI-65 The key step opens a modal
The key step of the site guide SHALL say: "If this is the first time you hand us a site, first register site access with a key", and SHALL have a link that opens a modal on the same page. The modal SHALL make the key (`POST /api/account/keys`) and show the `dalan login` command once, with Copy; it SHALL NOT leave the page. When the user already has a key the step SHALL be green and the link SHALL say "Make another key".

#### Scenario: Make a key in the guide
- **WHEN** the user presses the link in the key step
- **THEN** a modal opens on the same page, the key is made, the command is shown once, and the page address does not change

### Requirement: CLI-66 A visitor logs in first
A visitor on `/guide/site` SHALL see no steps, only "Log in first, then the steps come" and a "Log in" button. After the login the same address SHALL show the steps (CLI-63).

#### Scenario: Visitor
- **GIVEN** nobody is logged in
- **WHEN** `/guide/site` opens
- **THEN** the page shows the log in line and button, and none of the steps

### Requirement: CLI-67 Neutral examples
No text, example, test or doc of the product SHALL name a personal or company project. Examples SHALL use `mysite` and `shop`. The default site name of the guide SHALL be made from the user's username, else from the user's name, as lower case letters and digits starting with a letter and at most 20 long, else `mysite`.

#### Scenario: Name from the user
- **GIVEN** the username is `Sara_Rad`
- **WHEN** the default site name is made
- **THEN** it is `sararad`

#### Scenario: No letters
- **GIVEN** the name is written in Persian only and there is no username
- **WHEN** the default site name is made
- **THEN** it is `mysite`

### Requirement: CLI-68 A new page starts at the top
When the person goes to another address of the site (a header or footer link, the menu, a button), the screen SHALL scroll to the top. A link with a `#part` SHALL scroll to that part instead. Back and Forward SHALL keep the browser's own scroll place. This holds on a phone too, where the header and footer links are far apart.

#### Scenario: Footer link from the bottom
- **GIVEN** the person scrolled to the bottom of the landing page
- **WHEN** they press "About us" in the footer
- **THEN** the About page opens at the top

#### Scenario: Link with a part
- **WHEN** the person presses a link to `/#plans`
- **THEN** the screen scrolls to the plans, not to the top

### Requirement: CLI-69 No zoom on a focused field
The page SHALL have the viewport `width=device-width, initial-scale=1` with no `maximum-scale` and no `user-scalable`, and the style sheet SHALL set a font size of at least 16px on every field, so a focused field never zooms the page. A person SHALL still be able to zoom with a pinch or a double tap (owner, 2026-11-25: people with poor eyesight need it).

#### Scenario: Pinch zoom stays
- **WHEN** the page head and the style sheet are read
- **THEN** the viewport says scale 1 with no `maximum-scale` or `user-scalable`, and no `touch-action` stops zoom

#### Scenario: Field size
- **WHEN** the style sheet is read
- **THEN** inputs, selects and text areas are at least 16px

### Requirement: CLI-84 A refused builder
The account screen SHALL show a refused user the note that the admin refused, with the request button. The admin screen SHALL show the state `refused` and an "approve" button for it. The texts SHALL exist in Persian and in English.

#### Scenario: Account screen
- **WHEN** a user in state `refused` opens the account screen
- **THEN** the screen shows the refused note and the request button

#### Scenario: Admin screen
- **WHEN** the admin opens a refused user
- **THEN** the screen says `refused` and has an "approve" button

### Requirement: CLI-56 About page
`/about` SHALL be a public page that says what Dalan does, in the current language only: Persian when Persian is on, English when English is on. It SHALL NOT name the owner. The footer of every public page SHALL link `/about` and `/contact`.

#### Scenario: One language at a time
- **WHEN** the about texts are read in Persian and in English
- **THEN** each has the same number of parts, the Persian has Persian letters and the English has none

#### Scenario: Footer links
- **WHEN** a public page is drawn
- **THEN** its footer links `/about` and `/contact`

#### Scenario: Which screen
- **WHEN** the address is `/about` or `/contact`, for a visitor and for a logged-in user
- **THEN** both get the about page and the contact page, and the server gives the page at both addresses

### Requirement: CLI-57 Contact page
`/contact` SHALL be a public page with an email field and a text box (at most `contact_max_length` characters, ADM-17) and a send button. For a visitor, or a logged-in user with no email, the email field SHALL be empty and required. For a logged-in user with an email it SHALL hold that email and be read only. After a send that works the text box SHALL be emptied and a short "sent" note shown; a failure SHALL show its error.

#### Scenario: Visitor
- **WHEN** a visitor opens the contact page
- **THEN** the email field is empty, required and not read only

#### Scenario: User with an email
- **GIVEN** the user logged in with `sara@example.com`
- **WHEN** the contact page is drawn
- **THEN** the email field holds `sara@example.com` and is read only

### Requirement: CLI-71 Plain buttons are visible in the light theme
In the light theme the border of a plain button (`button`, `a.button`, the login way buttons) SHALL use the colour `--control-line`, with a contrast of at least 3:1 against `--surface` and against `--bg` (WCAG 2.2, 1.4.11). Cards, rows and dividers SHALL keep `--line`. Primary, link and coloured buttons, and the dark theme, SHALL NOT change.

#### Scenario: Border contrast
- **WHEN** the light `:root` colours of `app.css` are read
- **THEN** the contrast of `--control-line` against `--surface` and against `--bg` is each at least 3:1

#### Scenario: Buttons use the strong border
- **WHEN** the login screen opens in the light theme
- **THEN** the SMS, GitHub and email buttons and every plain button have a border of `--control-line`, and the card keeps its `--line` border

#### Scenario: Dark theme unchanged
- **WHEN** the theme is dark
- **THEN** plain buttons look as before (`--control-line` is the dark `--line`)

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

### Requirement: CLI-52 Reply in the chat
The user SHALL start a reply by a message's menu (right-click, or on a touch screen a long press of half a second with no move) → Reply or Copy, a double-click, or a swipe to the left; a long press SHALL NOT open the system's text menu. A bar above the input SHALL show the quote, with ✕ and Esc to cancel; the next message or file SHALL carry `reply_to` (REL-52). A replying message SHALL show the quote on top; a click on it SHALL go to that message like CLI-14.

#### Scenario: Reply to an answer
- **GIVEN** a bot answer in the chat
- **WHEN** the user opens its menu and picks Reply, then sends "ok"
- **THEN** the message "ok" has `reply_to` of that answer and shows its quote

#### Scenario: Long press on a phone
- **GIVEN** a touch screen
- **WHEN** the user holds a finger on a bot message for half a second without moving
- **THEN** the menu with Reply and Copy opens there, and Copy puts the message text on the clipboard

#### Scenario: A scroll is not a long press
- **WHEN** the finger moves more than 10 pixels before the time
- **THEN** no menu opens

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
On a bot's chat (`/@username`) the page SHALL link `/@username/manifest.webmanifest`, its `rel="icon"` and `rel="apple-touch-icon"` SHALL be `/@username/icon` (REL-54) and its title the bot's name; elsewhere Dalan's. In a browser tab, going between a bot's chat and any other screen (or another bot) SHALL load the page anew, since Safari reads the manifest only at load. So Install and "Add to Home Screen" there have the bot's address, icon and name.

#### Scenario: Bot page
- **WHEN** the chat of @mofidbot is shown
- **THEN** the manifest link is `/@mofidbot/manifest.webmanifest`, both icon links are `/@mofidbot/icon`, and the title is the bot's name

#### Scenario: Back to the list
- **WHEN** the user goes from the chat to `/chats`
- **THEN** the manifest link is `/manifest.webmanifest`, the icon link is `/icons/icon.svg` and the apple-touch-icon link is `/icons/icon-192.png`

#### Scenario: Into a chat in a browser tab
- **GIVEN** a browser tab, not an installed app, on `/dashboard/chats`
- **WHEN** the user opens the chat of @mofidbot, or goes back from it
- **THEN** the page loads anew at the new address

#### Scenario: In an installed app
- **GIVEN** the installed app
- **WHEN** the user opens a chat
- **THEN** the page does not load anew

### Requirement: CLI-72 Copyright and version in the footer
Under the footer columns of every public page SHALL be one small grey line: `© <this year> Dalan <version>` in English and `© <this year in Persian digits> دالان <version>` in Persian. `<version>` (OPS-17) SHALL come from `GET /api/public/settings` (`version`), because `/health` is private (OPS-06); it SHALL keep Latin digits and read left to right in both languages. Without a version like `v1.1.0` (no answer, or a local run) the line SHALL have none.

#### Scenario: Live version
- **GIVEN** the public settings give `version` `v1.1.0` in 2026
- **WHEN** an English public page opens
- **THEN** the footer line is `© 2026 Dalan v1.1.0`

#### Scenario: Persian
- **WHEN** the same page opens in Persian
- **THEN** the line is `© ۲۰۲۶ دالان v1.1.0`, with the version left to right

#### Scenario: No version
- **WHEN** the public settings fail, or give `dev`
- **THEN** the line is `© <this year> Dalan` with no version

### Requirement: CLI-73 Footer at the bottom
On a public page shorter than the screen, the footer SHALL sit at the bottom of the screen; on a longer page it SHALL come after the content. The footer SHALL NOT be fixed while scrolling and SHALL NOT cover content, at every screen size (320px to wide, portrait and landscape).

#### Scenario: Short page
- **WHEN** a page with little content (for example `/contact`) opens on a 1920 by 1080 screen
- **THEN** the bottom of the footer is at the bottom of the screen, and the free space is above the footer

#### Scenario: Long page
- **WHEN** the landing page opens on a 360 by 740 phone
- **THEN** the footer comes after the last section, and only shows after scrolling to the end

### Requirement: CLI-74 Command suggestions while typing
When the input starts with `/` and has no space, the client SHALL list above it the bot's commands (REL-26) whose name starts with the typed text, ignoring case, in the bot's order, each with `/command` and its description. A click, a tap or Enter on the marked row SHALL send `/command`; Tab SHALL put `/command ` in the input. The list SHALL close when nothing matches, after a space, when the input is empty, and when a message is sent.

#### Scenario: Slash shows all
- **GIVEN** the bot set the commands "start", "ticket" and "help"
- **WHEN** the user types `/`
- **THEN** the list shows `/start`, `/ticket` and `/help` with their descriptions, in that order

#### Scenario: Narrow by the start
- **WHEN** the user types `/TiC`
- **THEN** the list shows only `/ticket`, marked

#### Scenario: Send by Enter or tap
- **WHEN** the list shows `/ticket` marked and the user presses Enter, or taps it
- **THEN** the message `/ticket` is sent, the input is empty and the list is closed

#### Scenario: Complete with Tab
- **WHEN** the list shows `/ticket` marked and the user presses Tab
- **THEN** the input is `/ticket ` and the list is closed

#### Scenario: Closed
- **WHEN** the user types `/xyz`, or `/ticket 12`, or text not starting with `/`, or the bot has no commands
- **THEN** no list is shown

#### Scenario: Move and close
- **WHEN** the list is shown and the user presses ↓, ↑ or Escape
- **THEN** the mark moves down or up from the first row, or the list closes; the page reads left to right in both languages

### Requirement: CLI-75 Connection ring
The client SHALL NOT show a bar or toast while its event stream (CLI-08) is down. Instead a thin ring SHALL turn around the bot's icon in the chat header, or around the Dalan logo on other screens, until the stream opens again. Hovering the icon SHALL show "Connected" or "Connecting…" in the page language, and a hidden `role="status"` text SHALL say the same to screen readers.

#### Scenario: Stream down in a chat
- **GIVEN** the chat of @mofidbot is open
- **WHEN** the event stream ends
- **THEN** a turning ring shows around the bot's icon, no bar shows at the top, and the page does not move

#### Scenario: Back
- **WHEN** the stream opens again
- **THEN** the ring goes away and the status text says "Connected"

#### Scenario: Other screens
- **WHEN** the stream is down on the chat list
- **THEN** the ring turns around the Dalan logo in the header

#### Scenario: Tooltip
- **WHEN** a mouse hovers the bot's icon in English, while connected and then while connecting
- **THEN** the tooltip is "Connected", then "Connecting…"

#### Scenario: Reduced motion
- **GIVEN** the device asks for reduced motion
- **WHEN** the stream is down
- **THEN** the ring is shown still and dashed

### Requirement: CLI-76 Readable small text on public pages
On public pages, no text other than code SHALL be smaller than `--text-small`: 13px in English, 14px in Persian. Section labels (`.label`: the line above a section title, product labels, footer column titles) SHALL use `--text-label`: 14px in English and 15px in Persian. Both sizes SHALL be tokens in `:root`, set again under `:lang(fa)`.

#### Scenario: Persian section label
- **WHEN** the landing page is shown in Persian
- **THEN** the label "امکانات" is 15px, in Vazirmatn, with no letter spacing

#### Scenario: English section label
- **WHEN** the landing page is shown in English
- **THEN** the section labels are 14px, uppercase

#### Scenario: Nothing below the floor
- **WHEN** the CSS rules used by public pages are read (code blocks aside)
- **THEN** no font size is below `--text-small`, and `.label`, `.frame-label`, `ul.guards`, `.branch .box`, `.tag` and `.soon` use the tokens

### Requirement: CLI-77 Settings area
The account screen SHALL be a settings area: a side menu and one section per page, each at its own address: `/account/profile`, `/account/security`, `/account/notifications`, `/account/preferences`, `/account/builder` (the CLI-23 state, for every user) and `/account/keys` (only an approved builder or an admin). `/account` SHALL open Profile at 760px and wider, and the menu list below. A section page SHALL show a breadcrumb, a title, a description and rows.

#### Scenario: Desktop
- **WHEN** a user opens `/account` in a 1280px wide browser
- **THEN** the side menu shows the Account group, the current item is Profile, and the page shows `Settings › Profile` with the username, phone and email rows

#### Scenario: One address per section
- **WHEN** the user picks "Login and security", then presses Back
- **THEN** the address was `/account/security`, and Back shows Profile again

#### Scenario: A row's button
- **WHEN** a row's value is changed
- **THEN** its button is primary, and plain again when the value is as saved

#### Scenario: Builder group
- **GIVEN** a user who is not an approved builder or an admin
- **WHEN** the settings area opens
- **THEN** the Building group has only Builder, with the request button, the waiting note or the phone note (CLI-23), and `/account/keys` opens Profile

#### Scenario: Login methods
- **GIVEN** a user with a password and a phone, no email and no GitHub
- **WHEN** Login and security opens
- **THEN** Password and Phone show "Connected", Email and GitHub show "Not connected" with "Set up" and "Link"

#### Scenario: Preferences
- **WHEN** the user picks "Dark" or "English" in Preferences
- **THEN** the theme or the language applies at once, and that radio card is the chosen one

#### Scenario: Phone
- **WHEN** `/account` opens on a 360px phone
- **THEN** it shows the menu as a list of rows; picking Notifications opens `/account/notifications` with "‹ Settings" at the start

#### Scenario: Quiet buttons
- **WHEN** the Profile page opens
- **THEN** no button on it is primary; after the username is changed, its "Save" is primary

### Requirement: CLI-78 Grouped user menu
The user menu SHALL open with the user's name and, in grey, the phone or the email. Under it SHALL come groups with an icon per item and a line between groups: My bots, Chats, My apps; then Dashboard; then Log out alone. Dashboard SHALL open `/dashboard` for everyone (owner, 2026-11-27); Settings and Admin are in the dashboard's side menu.

#### Scenario: Admin
- **GIVEN** an admin with the email `a@example.com` and no phone
- **WHEN** the user menu opens
- **THEN** it shows the name, `a@example.com` in grey, then three groups: (My bots, Chats, My apps), (Dashboard), (Log out), each item with an icon, and Dashboard opens `/dashboard`

#### Scenario: Not an admin
- **WHEN** a user who is not an admin opens the menu
- **THEN** the second group has only Dashboard, which opens `/dashboard`

### Requirement: CLI-80 Line microphone on the voice button
The voice button of a bot chat (CLI-46) SHALL show Dalan's line `mic` icon (an inline SVG in `currentColor`: a capsule, an arc under it and a short stem), not an emoji. Its screen reader name SHALL stay "Record".

#### Scenario: Voice button
- **WHEN** a bot chat opens with an empty input
- **THEN** the voice button holds an `svg.icon` with the three `mic` paths, has no emoji text, and its `aria-label` is "Record"

### Requirement: CLI-81 One dashboard for signed-in pages
Every signed-in page except a bot's chat SHALL be in one dashboard frame at `/dashboard/...` with a side menu in groups: Workspace (Chats; My bots and My apps for a builder or an admin), Account (the sections of CLI-77), and Admin only for an admin. The old addresses (`/account/*`, `/chats`, `/bots`, `/apps`, `/admin`) SHALL redirect to their new place. An admin page opened by a user who is not an admin SHALL show "Not found".

#### Scenario: Common user
- **GIVEN** a user who is not a builder or an admin
- **WHEN** the dashboard opens
- **THEN** the menu has Workspace with Chats only, and Account; there is no Admin group

#### Scenario: Admin
- **GIVEN** an admin
- **WHEN** the dashboard opens
- **THEN** the menu also has the Admin group with its nine items: Overview, Requests, Users, Bots, Showcase, Messages, Settings, Access log, Audit log

#### Scenario: Typed admin address
- **GIVEN** a user who is not an admin
- **WHEN** the user opens `/dashboard/admin/settings`
- **THEN** the page says "Not found" and shows no admin data

#### Scenario: Old address
- **WHEN** a user opens `/admin` or `/bots`
- **THEN** the address becomes `/dashboard/admin` or `/dashboard/bots`

#### Scenario: The menu takes every click
- **GIVEN** a wide window and a workspace page with a list (My bots with two bots)
- **WHEN** the user moves the pointer over a side menu item and clicks it
- **THEN** the item is the top element there, and its page opens

### Requirement: CLI-82 Admin overview
The first admin page SHALL show cards with counts, each a link to its page: users (and new in the last 7 days), bots (and online now), builder and showcase requests waiting, unread support messages, sites online.

#### Scenario: Waiting requests
- **GIVEN** two builder requests and one showcase request wait
- **WHEN** an admin opens the overview
- **THEN** the requests card says 3 and opens Requests

### Requirement: CLI-83 Readable admin settings
The admin settings page SHALL group the settings by service and show each as a setting row: name and help in the page language, its key in small grey text, the control by kind (a number with unit and range, a switch, a text), "Changed" with "Reset", and "Needs restart". Save SHALL be primary only when the value changed. A search box SHALL filter rows. A risky setting SHALL ask before it saves.

#### Scenario: Readable row
- **WHEN** the Admin group of settings is shown in English
- **THEN** `AccessLogRetentionDays` shows as "Access log kept for" with "days", its help, and its key in grey

#### Scenario: Search
- **WHEN** the admin types "log" in the search box
- **THEN** only rows whose name, key or help has "log" stay

#### Scenario: Changed and restart
- **GIVEN** a setting whose value differs from its start value, and one that applies only after a restart
- **WHEN** the settings page shows them
- **THEN** the first has "Changed" with its start value and "Reset", and the second has "Needs restart"

#### Scenario: Risky
- **WHEN** the admin changes the Bot API address and presses Save
- **THEN** a question shows the old and the new value, and nothing saves until the admin confirms

### Requirement: CLI-92 Second factor in the app
"Login and security" (CLI-77) SHALL have a second-factor card: Set up shows the QR (from the server), the secret to copy and a code box; a right code shows the recovery codes once with Copy and Download. When a login answers `totp_required`, the login screen SHALL ask for the code (or a recovery code) and then finish the login. For an admin the card SHALL have no Turn off.

#### Scenario: Set up
- **WHEN** the user presses Set up and types the code from the authenticator app
- **THEN** the card says it is on, and the ten recovery codes show once

#### Scenario: Login step
- **GIVEN** a password login answered `totp_required`
- **WHEN** the user types a right code
- **THEN** the login finishes as usual

### Requirement: CLI-91 Offline bar in the chat
While the bot of the open chat is offline (its status, CLI-19, has `online` false), the chat SHALL show a bar above the input. The bar SHALL name the bot as `@username`, say it will not answer now, and say messages wait and show "not delivered" if it does not come back in time. It SHALL NOT show for an online or suspended bot (CLI-25). Sending is not blocked.

#### Scenario: Bot offline
- **GIVEN** the chat of `@mofid` is open and its status is `online` false, with or without `last_seen`
- **WHEN** the status is drawn in English
- **THEN** a bar above the input says "@mofid is offline and won't answer now" and mentions "not delivered"

#### Scenario: Bot comes back
- **GIVEN** the offline bar is shown
- **WHEN** the next status is `online` true
- **THEN** the bar is hidden

#### Scenario: Status call fails
- **WHEN** a status call fails
- **THEN** the bar stays as it was (hidden before the first status)

#### Scenario: Suspended
- **WHEN** a suspended bot is offline
- **THEN** the bar is hidden; the suspended error shows instead

### Requirement: CLI-85 Enter like Telegram
In the message box and in the big editor (CLI-87), the client SHALL decide Enter by the pointer, read from `matchMedia("(pointer: coarse)")` at each key press and never from the user agent. Fine pointer: Enter SHALL send; Shift+Enter or Ctrl+Enter SHALL add a new line. Coarse pointer: Enter SHALL add a new line, and only the Send button SHALL send. While an IME is composing (`isComposing`), Enter SHALL never send.

#### Scenario: Computer
- **GIVEN** the pointer is fine and the box has "Hi"
- **WHEN** the user presses Enter
- **THEN** "Hi" is sent

#### Scenario: New line on a computer
- **GIVEN** the pointer is fine
- **WHEN** the user presses Shift+Enter, then Ctrl+Enter
- **THEN** nothing is sent and the box has two new lines, each removed by one undo

#### Scenario: Phone
- **GIVEN** the pointer is coarse and the box has "Hi"
- **WHEN** the user presses Enter
- **THEN** nothing is sent and the box has "Hi" and a new line; the Send button sends it

#### Scenario: IME
- **GIVEN** the pointer is fine and an IME is composing
- **WHEN** the user presses Enter
- **THEN** nothing is sent

### Requirement: CLI-86 The message box grows
The message box SHALL grow with its text up to `--compose-max-height` (about six lines, at most 30% of the screen height, like Telegram) and then scroll. Its buttons SHALL keep their size at the bottom. After a send it SHALL be one row high again.

#### Scenario: Long text
- **WHEN** the user types 30 lines
- **THEN** the box is about six lines high and scrolls, and Send stays its own size at the bottom

#### Scenario: After send
- **WHEN** the user sends 5 lines
- **THEN** the box is one row high and empty

#### Scenario: No scrollbar before the largest size
- **WHEN** the box is empty, or holds one to five short lines
- **THEN** the box shows no scrollbar

#### Scenario: Only the message list scrolls
- **WHEN** a chat is open on a computer or a phone
- **THEN** the page itself is never taller than the window; only the message list (and the box past its largest size) scrolls

### Requirement: CLI-87 Big editor
Over 3 lines of text (wrapped lines count), a small expand button (two diagonal arrows) SHALL show in the box's corner. It SHALL open a modal with the same box: full screen on a coarse pointer or under 600px, otherwise large in the middle. It SHALL have Send and Close; Close and Esc SHALL keep the text, selection and undo. CLI-85 SHALL apply in it. Ctrl+B and Ctrl+I (Cmd on macOS) SHALL put `**` and `_` around the selection.

#### Scenario: Button shows
- **WHEN** the box has 3 lines, then 4
- **THEN** the expand button is hidden, then shown

#### Scenario: Same text
- **GIVEN** the box has 4 lines
- **WHEN** the user opens the editor, adds a line, and presses Esc
- **THEN** the box has the 5 lines and the editor is closed

#### Scenario: Send from the editor
- **WHEN** the user presses Send in the editor
- **THEN** the text is sent, the editor is closed, and the box is empty and one row high

#### Scenario: Bold
- **GIVEN** "hello world" with "world" selected in the editor
- **WHEN** the user presses Ctrl+B
- **THEN** the text is "hello **world**"

### Requirement: CLI-88 A draft per chat
Each chat SHALL keep its unsent text when the user opens another chat or reloads the page, kept only in this browser for this user. A send SHALL remove the draft; logout SHALL remove every draft. The box SHALL keep `dir="auto"`, the font and the theme.

#### Scenario: Switch chats
- **GIVEN** the user typed "half" in the chat with bot A
- **WHEN** they open bot B, then bot A again
- **THEN** bot B's box is empty and bot A's box has "half"

#### Scenario: Logout
- **GIVEN** drafts in two chats
- **WHEN** the user logs out
- **THEN** no draft is left in the browser

### Requirement: CLI-89 Photos in the chat
A photo, and a document of type `image/jpeg`, `image/png`, `image/gif` or `image/webp`, SHALL show as a picture in its message. A tap or click SHALL open a full-screen viewer with pinch zoom, double-tap zoom, pan while zoomed, Download and Close. Esc SHALL close it. Any other image type (for example SVG) SHALL stay a download.

#### Scenario: Picture sent as a document
- **WHEN** a bot sends `chart.png` with `sendDocument`
- **THEN** the message shows the picture, not a link

#### Scenario: SVG
- **WHEN** a bot sends `logo.svg` of type `image/svg+xml`
- **THEN** the message shows a download link

#### Scenario: Open and close
- **WHEN** the user taps the picture, then presses Esc
- **THEN** the viewer opens with Download and Close, then closes and the focus is back on the message

#### Scenario: Double tap
- **WHEN** the user double-taps the open picture, then double-taps again
- **THEN** it zooms in at that point, then back to its full size

### Requirement: CLI-90 HTML files open in the app
A document of type `text/html`, or of no clear type named `.html` or `.htm`, SHALL show as a card with its name, size and "Open". Open SHALL get a view link (REL-57) and show it full screen in the app in `<iframe sandbox="allow-scripts allow-popups">`, never `allow-same-origin`. The viewer SHALL have Close, Download and "Open in a new tab"; Esc SHALL close it. The frame SHALL get the focus on load.

#### Scenario: Card
- **WHEN** a bot sends `sprint-review.html` of 1.2 MB
- **THEN** the message shows a card "sprint-review.html", "1.2 MB" and "Open"

#### Scenario: Open on a computer
- **GIVEN** the Fullscreen API works
- **WHEN** the user presses Open
- **THEN** the viewer is full screen with the frame focused, and the frame's `sandbox` is exactly `allow-scripts allow-popups`

#### Scenario: Keys and swipes reach the page
- **WHEN** the viewer has loaded the file
- **THEN** the frame has the focus, and the page's own keys (← →) and swipes work in it

#### Scenario: Open on an iPhone
- **GIVEN** the Fullscreen API does not work
- **WHEN** the user presses Open
- **THEN** a fixed layer covers the screen inside the safe areas, and the chat under it does not scroll

#### Scenario: Turn a page
- **GIVEN** the sample report is open in the viewer, in WebKit as an iPhone and in Chromium
- **WHEN** the user presses → and then swipes left
- **THEN** the report shows its second and then its third page

#### Scenario: Close
- **WHEN** the user presses Close, or Esc, or leaves full screen
- **THEN** the viewer and its frame are gone and the chat is as before

#### Scenario: Viewer off
- **GIVEN** no view host is set
- **WHEN** a bot sends `page.html`
- **THEN** the card shows Download and no Open

### Requirement: CLI-79 Calendar choice
Preferences (CLI-77) SHALL let the user show dates in the Persian (Jalali) or the Gregorian calendar, as two radio cards with today's date as the example. The choice SHALL apply at once to every date the client shows and be kept on the device. With no choice, Persian SHALL use Jalali and English Gregorian.

#### Scenario: Default
- **WHEN** a key's expiry is shown in Persian with no choice made
- **THEN** it is a Jalali date in Persian digits

#### Scenario: Gregorian in Persian
- **GIVEN** the user chose Gregorian
- **WHEN** the same date is shown in Persian
- **THEN** it is a Gregorian date with Persian month names and digits

### Requirement: CLI-93 Admin tables and detail pages
Users and Bots in the admin dashboard SHALL open as tables of everyone, newest first, with "More" for the next page and a search box above that narrows them. Users: name and username, phone or email, builder state, bots, joined, last active, a suspended mark. Bots: name, username, owner, chat users, last seen, a suspended mark. A row SHALL open `/dashboard/admin/users/{id}` or `/dashboard/admin/bots/{id}`: the details, the actions, and the user's bots or the bot's owner as links.

#### Scenario: Users without a search
- **GIVEN** five users
- **WHEN** an admin opens Users
- **THEN** a table shows all five, the newest first, with no search typed

#### Scenario: A user's page
- **WHEN** an admin clicks the row of user 7, who owns two bots
- **THEN** the address is `/dashboard/admin/users/7`, the page shows the user's details and actions, and two bot rows that open their pages

#### Scenario: Not an admin
- **WHEN** a user who is not an admin opens `/dashboard/admin/users/7`
- **THEN** the page is "Not found"

### Requirement: CLI-94 Colored titles on public pages
The accent line of each big title on public pages (the home page hero and section titles) SHALL be drawn in a gradient of five colors (`--rainbow`, its own set for the dark theme), as text. The gradient SHALL move slowly along the text, like ngrok's titles; with `prefers-reduced-motion` it SHALL stand still. In Persian it SHALL run from the right.

#### Scenario: Home page title
- **WHEN** the home page opens
- **THEN** the second line of the hero title is the colored gradient, moving slowly

#### Scenario: Reduced motion
- **GIVEN** the system asks for reduced motion
- **WHEN** the home page opens
- **THEN** the title is colored and does not move

### Requirement: CLI-95 A new email login says it makes an account
When the email login's code is right but the address belongs to no account (the server asks for a name), the login screen SHALL say, above the name box, that this makes a new account, and that someone who already has an account with a mobile number SHALL log in with it and add the email in Profile instead. The same note SHALL show for a GitHub login that would make a new account.

#### Scenario: New address
- **GIVEN** no account has `sara@example.com`
- **WHEN** Sara enters the right email code
- **THEN** the name box shows with the note that a new account will be made and how to add the email to an existing one

### Requirement: CLI-96 The bell
Every signed-in page SHALL have a bell in the header with the unread count (none when 0). It SHALL open a panel with the newest notices (unread ones bold) and "More"; opening it SHALL mark them read; a notice with a link SHALL open it. A push for a notice SHALL open its link (CLI-22). The count SHALL refresh with the live stream, or on each screen change.

#### Scenario: Unread
- **GIVEN** two unread notices
- **WHEN** the user opens any signed-in page
- **THEN** the bell shows 2; after the panel opens it shows none

#### Scenario: Open a notice
- **WHEN** the admin taps the notice of a new site
- **THEN** that user's admin page opens

### Requirement: CLI-97 Login with Dalan for builders
Each site in My apps SHALL have "Login with Dalan": a switch, the client id, a "New secret" button that shows the secret once with Copy, the discovery address, and a switch "Require login on the proxy" (SITE-26). `/guide/login`, in both languages, SHALL explain both ways step by step with the addresses from the public settings and short examples (Node, Python, PHP, PocketBase); `/llms.txt` SHALL have the same.

#### Scenario: Turn on in My apps
- **WHEN** the owner of `shop` turns on Login with Dalan
- **THEN** the card shows client id `shop`, the secret once with Copy, and the discovery address

#### Scenario: The guide
- **WHEN** a visitor opens `/guide/login` in Persian
- **THEN** it shows both ways with this server's addresses and the examples

### Requirement: CLI-98 Toasts do not pile up
A toast with the same text as one already shown SHALL replace it, so the same text is never shown twice at once. At most 3 toasts SHALL be shown at once; a new one SHALL remove the oldest.

#### Scenario: The same error on each screen
- **GIVEN** a slow network where each screen's call fails
- **WHEN** the user opens five dashboard pages in a row
- **THEN** one "Failed" toast is shown, and the menu items under the toast area can be tapped

#### Scenario: Many different toasts
- **WHEN** four different toasts come one after another
- **THEN** only the last three are shown

### Requirement: CLI-99 The Backup page
The admin part of the dashboard SHALL have a "Backup" item at `/dashboard/admin/backup`: the list of backups (time, size, uploaded) with Download and Restore for each, "Back up now", "Upload a backup", and the last results. Restore SHALL ask the admin to type `RESTORE` and say that everyone, the admin too, must log in again after it.

#### Scenario: The menu
- **WHEN** an admin opens the dashboard
- **THEN** the admin group has "Backup" at `/dashboard/admin/backup`; for anyone else that address is "Not found"
