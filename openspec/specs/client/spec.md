# client Specification

## Purpose
The web app people use: a small PWA in plain TypeScript, Persian first, served by the same process, that loads nothing from a foreign host.

## Requirements

### Requirement: CLI-01 The server serves the client
The server SHALL serve the PWA page at `/`, `/bots`, `/privacy`, `/faq`, `/showcase` and `/@<bot username>`, and its files (scripts, styles, font, icons) from the same origin. The page SHALL be sent with `Cache-Control: no-cache`, so a new release is seen at the next open. Any other unknown path SHALL get 404.

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response is 200 HTML with `Cache-Control: no-cache`

#### Scenario: Open a bot link
- **WHEN** a browser asks for `/@ArcaneBot`
- **THEN** the response is the same page

#### Scenario: Open a public page
- **WHEN** a browser asks for `/privacy`, `/faq` or `/showcase`
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
The client SHALL show under the input the reply keyboard of the newest bot message that carries one. An empty keyboard removes the buttons. A tap on a button SHALL send its text as a message from the user.

#### Scenario: Keyboard from the newest message that has one
- **GIVEN** bot message 2 has a keyboard and bot message 4 has none
- **WHEN** the chat is shown
- **THEN** the buttons of message 2 are shown

#### Scenario: Keyboard removed
- **GIVEN** bot message 2 has a keyboard and bot message 4 has an empty keyboard
- **WHEN** the chat is shown
- **THEN** no buttons are shown

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
The page SHALL link a web app manifest with a name, icons, `display` "standalone" and a start address on the same origin, and SHALL register a service worker from the same origin.

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
The client SHALL show the inline keyboard of a message under that message. A tap on a button with `callback_data` SHALL send the press (REL-21); a tap on a `url` button SHALL open the link in a new tab without the site as referrer. An `answer` event with a text SHALL be shown: as an alert when `show_alert` is true, otherwise as a short note.

#### Scenario: Buttons of a message
- **GIVEN** message 2 has the inline buttons `[[A],[B]]`
- **WHEN** the chat is shown
- **THEN** message 2 has two rows of buttons under it

### Requirement: CLI-14 Pinned message bar
The client SHALL show the pinned message of the chat (REL-25) in a bar above the messages, taken from the `pinned` field of the newest messages answer and kept up to date by changes: a message with `pinned` true becomes the bar; the bar's message without `pinned`, or deleted, clears it.

#### Scenario: Pin moves
- **GIVEN** the bar shows message 2
- **WHEN** message 2 arrives without `pinned` and then message 3 with `pinned` true
- **THEN** the bar shows message 3

#### Scenario: Unpin
- **GIVEN** the bar shows message 2
- **WHEN** message 2 arrives without `pinned`
- **THEN** there is no bar

### Requirement: CLI-15 Command menu
When the bot has commands (REL-26), the client SHALL show a menu button by the input. Choosing a command SHALL send `/<command>` as a message.

#### Scenario: Menu
- **GIVEN** the bot set the commands "start" and "help"
- **WHEN** the user opens the menu and picks "help"
- **THEN** the message "/help" is sent

### Requirement: CLI-16 Files in the chat
The client SHALL let the user pick a file and send it with the text in the input as its caption (REL-28), and refuse a file over `max_file_size_mb` (REL-18) before sending. A message with a `file` SHALL show a photo as an image, audio and voice with an audio player, video with a video player, and anything else as a link with its name and size.

#### Scenario: Kind of view
- **WHEN** messages with a photo, a voice, a video and a document are shown
- **THEN** they use an image, an audio player, a video player and a link

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
The account screen SHALL show the user's builder state: a button "become a builder" when it is `none`, "waiting for approval" when `requested`, and a button that opens "My bots" when `approved` or for an admin. A user with no phone SHALL see, instead of the request button, a note that building needs a verified mobile number (CLI-34). Admins SHALL also see a button that opens the admin screen.

#### Scenario: What the account screen shows
- **WHEN** the builder state is `none`, `requested` or `approved`, or the user is an admin
- **THEN** the screen shows the request button, the waiting note, the "My bots" button, or the "My bots" and "Admin" buttons

#### Scenario: No phone
- **WHEN** a user with no phone and builder state `none` opens the account screen
- **THEN** the screen shows the "verify your mobile first" note and no request button

### Requirement: CLI-24 My bots
The "My bots" screen SHALL list the builder's bots (username, name, last seen, suspended) and have a form to create one. The bot list SHALL have a "Create bot" button for every user: it opens "My bots" for a builder or an admin, and the account screen (CLI-23) for anyone else. After a create or a new token the screen SHALL show the token once, with a copy button. Each bot SHALL have Rename (REL-48), New token and Delete (REL-49); New token and Delete SHALL ask first.

#### Scenario: Token shown once
- **WHEN** a builder creates a bot
- **THEN** the token is shown with the copy button, and after closing it the list shows the bot without the token

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
A visitor and a logged-in user SHALL see at `/` a landing page in the current theme (CLI-35): a "Problems we solve" menu; a hero with "Enter"; product cards with a traffic picture (bot relay, where the computer only calls out; sites, marked soon); OS tabs with real commands built from `site_url`; three steps using `bot_api_url`; the three messages of DESIGN section 13 in order; a security list; a closing call; a footer. It SHALL NOT say "tunnel".

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by email or SMS without building a login; keeps working when international internet is cut. None says "tunnel"

#### Scenario: Diagram parts
- **WHEN** the bot card picture is read in Persian or English
- **THEN** it goes from the users through Dalan to your computer, and the link to the computer says the call goes out from the computer only

#### Scenario: Steps use the real addresses
- **GIVEN** the public settings say `bot_api_url` is `https://api.example.ir` and `site_url` is `https://example.ir`
- **WHEN** the steps are made in Persian and in English
- **THEN** there are three steps, the second shows `https://api.example.ir/bot<token>/getUpdates`, the third shows `https://example.ir/@`, and no step says "tunnel"

#### Scenario: Which screen
- **WHEN** the address is `/`, `/bots`, `/privacy`, `/faq`, `/showcase` or `/@bot`, for a visitor and for a logged-in user
- **THEN** a visitor gets landing, login, privacy, FAQ, showcase and login; a logged-in user gets landing, the bot list, privacy, FAQ, showcase and the chat

#### Scenario: Drawn in the page
- **WHEN** the landing page is drawn
- **THEN** the diagram and icons are made in the page, nothing is loaded from another host, and the moving dots stop when the visitor asks for reduced motion

#### Scenario: Try it on every system
- **GIVEN** the public settings say `site_url` is `https://example.ir`
- **WHEN** the commands are made
- **THEN** there are tabs for Linux, macOS, Windows and Docker, and each command gets `https://example.ir/dalan.mjs`

#### Scenario: Only real things are offered
- **WHEN** the menu and the product cards are read
- **THEN** every item that is not built yet is marked "soon", and no text says "tunnel"

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
"My bots" SHALL have a part for AI agents: the builder's keys (name, made, expires) with a delete button, a form to make a key that shows the new key once with a copy button, and a ready prompt to copy. The ready prompt SHALL name the site address, the Bot API address, `/llms.txt`, the MCP door and the CLI, with the addresses from the public settings (ADM-15), and SHALL never hold a key.

#### Scenario: Ready prompt
- **GIVEN** the public settings say site `https://dalanet.ir` and Bot API `https://api.dalanet.ir`
- **WHEN** the ready prompt is made in Persian and in English
- **THEN** it has `https://dalanet.ir/llms.txt`, `https://dalanet.ir/api/mcp`, `https://dalanet.ir/dalan.mjs` and `https://api.dalanet.ir`

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
The client SHALL have a light and a dark theme. On the first open it SHALL follow the device's theme. The public pages and the bot list SHALL have a theme button that switches only between light and dark; the choice SHALL be kept on this device and used on the next open, whatever the device says. The switch SHALL show the new theme as a circle that grows from the button, and SHALL change at once when the visitor asks for reduced motion. Code blocks SHALL stay dark in both themes.

#### Scenario: First open follows the device
- **WHEN** a visitor opens the site for the first time on a device that asks for dark
- **THEN** the theme is dark (`data-theme="dark"`), and nothing is kept yet

#### Scenario: Choice is kept
- **WHEN** the visitor presses the theme button twice, starting from light
- **THEN** the theme is dark, then light; there is no third state, and the next open uses the kept choice

### Requirement: CLI-36 One simple login card
The login screen SHALL be one card, like ngrok: a "Log in with GitHub" button that opens `/api/auth/github` (only when `github_login` is on), a line "or", then the email form (or the SMS form when `email_login` is off), and at the bottom small links to the other ways. When the address has `login_error` (AUTH-29) the card SHALL show its text and remove it from the address. The GitHub mark SHALL be inline.

#### Scenario: GitHub on
- **GIVEN** `github_login` is true
- **WHEN** the login screen opens
- **THEN** the first button is "Log in with GitHub" and links to `/api/auth/github`, and the email field is below it

#### Scenario: GitHub off
- **GIVEN** `github_login` is false
- **WHEN** the login screen opens
- **THEN** there is no GitHub button and no "or" line

#### Scenario: Error from GitHub
- **WHEN** the page opens at `/?login_error=github_unavailable`
- **THEN** the login card shows "GitHub cannot be reached now. Log in by email or SMS." in the person's language and the address becomes `/`

#### Scenario: Texts
- **WHEN** the error is `github_state`, `github_denied`, `github_no_email`, `github_unavailable` or `suspended`
- **THEN** it has its own text, in Persian and in English

### Requirement: CLI-37 What you get
The landing page SHALL have a "What you get" section after the product cards: four groups in order (bot relay, users and safety, tools for builders, sites and gateway), each with a title, a short text and a list. The first three SHALL list only built things; every item of the fourth SHALL be marked "soon". One line SHALL say what Dalan does not offer. No text SHALL say "tunnel".

#### Scenario: Ready and soon
- **WHEN** the groups are read in Persian or English
- **THEN** there are four, no thing in the first three is marked soon, and every thing in the fourth is marked soon

#### Scenario: Honest about what is not offered
- **WHEN** the not-offered line is read in Persian or English
- **THEN** it names TCP, SSH and the AI gateway, and no text of the section says "tunnel"
