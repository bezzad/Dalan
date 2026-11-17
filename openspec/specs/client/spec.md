# client Specification

## Purpose
The web app people use: a small PWA in plain TypeScript, Persian first, served by the same process, that loads nothing from a foreign host.

## Requirements

### Requirement: CLI-01 The server serves the client
The server SHALL serve the PWA page at `/` and at `/@<bot username>`, and its files (scripts, styles, font, icons) from the same origin. The page SHALL be sent with `Cache-Control: no-cache`, so a new release is seen at the next open. Any other unknown path SHALL get 404.

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response is 200 HTML with `Cache-Control: no-cache`

#### Scenario: Open a bot link
- **WHEN** a browser asks for `/@ArcaneBot`
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
The client SHALL keep the messages of a chat in rising `id` order with each `id` once, no matter whether a message arrived live, by sync, as the answer to a send, or more than once.

#### Scenario: Same message twice
- **GIVEN** a list with messages 1 and 2
- **WHEN** message 2 arrives again and then message 3
- **THEN** the list is 1, 2, 3

#### Scenario: Out of order
- **WHEN** message 5 arrives before message 4
- **THEN** the list shows 4 before 5

### Requirement: CLI-06 Sync on open and on reconnect
When a chat is opened the client SHALL load the newest messages. Each time the event stream opens again the client SHALL ask for the messages after the newest `id` it has, and SHALL ask again while a full page (`sync_page_size`, REL-18) comes back. A live message that arrives during a sync SHALL NOT make the sync skip older messages.

#### Scenario: Open a chat
- **WHEN** a chat is opened with no messages on the device
- **THEN** the client asks for the newest messages (no `after`)

#### Scenario: Reconnect
- **GIVEN** the newest message on the device has `id` 7
- **WHEN** the event stream opens again
- **THEN** the client asks for the messages after 7

#### Scenario: More than one page was missed
- **GIVEN** a sync answer is a full page
- **WHEN** it arrives
- **THEN** the client asks again after the newest `id` of that page, until a page is not full

#### Scenario: Live message during a sync
- **GIVEN** the newest message on the device has `id` 7 and a sync is running
- **WHEN** message 12 arrives live before the sync answer
- **THEN** the sync still asks after 7 and the messages 8 to 11 are not lost

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
