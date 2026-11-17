## ADDED Requirements

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
