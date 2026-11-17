## MODIFIED Requirements

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

## ADDED Requirements

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
