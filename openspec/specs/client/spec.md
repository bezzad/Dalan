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
The login screen SHALL be one card with one equal button per way: SMS, GitHub (`/api/auth/github`, when `github_login` is on) and email (when `email_login` is on). Without `dalan.returning` in local storage the order SHALL be SMS, GitHub, email; after any login the client SHALL set it, and the order SHALL be GitHub, email, SMS. SMS and email SHALL open their form with a "back" button. Password and dev login SHALL be small links. A `login_error` (AUTH-29) SHALL show and leave the address.

#### Scenario: New browser
- **GIVEN** `github_login` and `email_login` are true and local storage has no `dalan.returning`
- **WHEN** the login screen opens
- **THEN** the buttons are, in order, SMS, GitHub, email

#### Scenario: Returning browser
- **GIVEN** the person logged in before on this browser
- **WHEN** the login screen opens
- **THEN** the buttons are, in order, GitHub, email, SMS

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
A visitor and a logged-in user SHALL see at `/guide` a choice of plan: a bot or a site. In both languages, `/guide/bot` SHALL show nine steps to run a bot without an AI agent and `/guide/site` four steps (CLI-39), with addresses and numbers from the public settings. "My bots" SHALL show steps 5 to 9 of the bot guide, numbered from 1. The token test SHALL show where the whole token goes; on "My bots" it SHALL hold the new token while that token is on screen.

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
- **GIVEN** "My bots" shows the new token `7:XyZ`
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
The user menu SHALL open "My apps" at `/apps`. For a builder it SHALL list their sites (name, address as a link, online or offline) with a delete that asks first, and show a step-by-step guide: 1) install the agent, 2) log in with a key made here by one button (the full `dalan login` command, shown once, with Copy), 3) run `dalan http <port> --name <name>`, with an example for two sites. Every action SHALL end in a toast. A user who is not a builder SHALL see how to become one instead.

#### Scenario: Guide commands
- **WHEN** the guide is drawn for the name `mysite` and port `3000`
- **THEN** it shows `dalan http 3000 --name mysite`

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
