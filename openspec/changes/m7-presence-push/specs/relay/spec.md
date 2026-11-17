## MODIFIED Requirements

### Requirement: REL-14 List my bots
`GET /api/bots` SHALL return `{"bots":[...]}` with every bot this user has a chat with, newest chat first, and no other bot. Each bot has `id`, `username`, `name`, `unread` (REL-32), `online` and `last_seen` (REL-33).

#### Scenario: Two users, two bots
- **GIVEN** user A wrote to bot 1 and user B wrote to bot 2
- **WHEN** user A calls `GET /api/bots`
- **THEN** the list holds bot 1 only

#### Scenario: No chat yet
- **WHEN** a new user calls `GET /api/bots`
- **THEN** the list is empty

#### Scenario: Unread and status in the list
- **GIVEN** a user wrote to a bot that never polled, and the bot then sent two messages
- **WHEN** the user calls `GET /api/bots`
- **THEN** that bot has `unread` 2, `online` false and `last_seen` null

### Requirement: REL-16 History and sync
`GET /api/bots/{bot id}/messages` SHALL return `{"messages":[...],"pinned":...}` of this user's chat with that bot only, in rising `id` order (rising `version` with `after`). `pinned` is the chat's pinned message or null. `after` gives the messages with a higher `version`; `before` those with a lower `id`; `limit` sets the size, at most `Relay:MaxMessagesPerSync` (start 100).

#### Scenario: Fields of a message
- **WHEN** the history is read
- **THEN** each message has `id`, `version`, `from` ("user" or "bot"), `text`, `date` and, when set, `reply_keyboard`, `inline_keyboard`, `file`, `edited`, `pinned`, `deleted` and `not_delivered`

#### Scenario: Open a chat
- **GIVEN** a chat with messages 1 to 5
- **WHEN** the user calls it with `limit=3` and no other parameter
- **THEN** the newest messages 3, 4 and 5 are returned in that order

#### Scenario: Limit not given or too big
- **GIVEN** the maximum is 2 and the chat has 5 messages
- **WHEN** the user calls it with no `limit`, or with `limit=50`
- **THEN** 2 messages are returned

#### Scenario: Sync after a number
- **GIVEN** a chat with messages 1 to 5 and no change
- **WHEN** the user calls it with `after=2&limit=2`
- **THEN** the messages 3 and 4 are returned (the oldest after version 2)

#### Scenario: Sync brings a change
- **GIVEN** a chat with messages 1 to 3, and the bot then edits message 1
- **WHEN** the user calls it with `after=3`
- **THEN** only message 1 is returned, with its new text and `version` 4

#### Scenario: Older messages
- **GIVEN** a chat with messages 1 to 5
- **WHEN** the user calls it with `before=4&limit=2`
- **THEN** the messages 2 and 3 are returned (the newest before 4)

#### Scenario: Another user's chat
- **GIVEN** user A and user B each have a chat with the same bot
- **WHEN** user A reads the messages of that bot
- **THEN** no message of user B is returned

#### Scenario: Bad parameter
- **WHEN** the user calls it with `after=abc`
- **THEN** the response is 400 with error code `bad_request`

### Requirement: REL-18 Client settings
`GET /api/settings` SHALL return the settings the client needs: `max_text_length` (`Relay:MaxTextLength`), `max_caption_length` (`Relay:MaxCaptionLength`), `max_file_size_mb` (`Relay:MaxFileSizeMb`), `sync_page_size` (`Relay:MaxMessagesPerSync`), `reconnect_min_seconds` and `reconnect_max_seconds` (`Relay:ClientReconnectMinSeconds`, start 1, and `Relay:ClientReconnectMaxSeconds`, start 30), and `status_refresh_seconds` (`Relay:ClientStatusRefreshSeconds`, start 30). The client SHALL NOT hard-code these values.

#### Scenario: Read the settings
- **GIVEN** `Relay:MaxTextLength` is 10
- **WHEN** a logged-in user calls `GET /api/settings`
- **THEN** the response has `max_text_length` 10, the caption and file limits, the page size, both reconnect values and `status_refresh_seconds`

## ADDED Requirements

### Requirement: REL-31 "Not delivered" is shown
When a user message becomes "not delivered" (Q-02, Q-04, Q-07), it SHALL get the next `version` of its chat and a `MessageChanged` event, in the same transaction. The message then has `not_delivered` true. A "delivered" message gets no new version.

#### Scenario: Expired while the bot is away
- **GIVEN** a user message with `version` 1 that the bot never read
- **WHEN** its expiry passes and the sweep runs
- **THEN** the message has `not_delivered` true and `version` 2, and a sync with `after=1` returns it

#### Scenario: Replaced by a newer command
- **WHEN** a user sends two messages before the bot reads
- **THEN** the first has `not_delivered` true and the second has no `not_delivered`

#### Scenario: Delivered stays quiet
- **WHEN** the bot reads and confirms a user message
- **THEN** the message keeps its `version` and has no `not_delivered`

### Requirement: REL-32 Read position and unread count
Each chat SHALL keep the user's read position. `POST /api/bots/{bot id}/read` with JSON `{"message_id": N}` SHALL move it to N, but never back and never past the newest message, and SHALL answer `{"unread": n}`. Unread is the number of bot messages of the chat, not deleted, with an `id` above the read position. A message the user sends SHALL move the read position to that message.

#### Scenario: Read up to a message
- **GIVEN** a chat with user message 1 and bot messages 2, 3 and 4
- **WHEN** the user reads up to 3
- **THEN** the answer has `unread` 1

#### Scenario: Never back
- **GIVEN** the user read up to 4
- **WHEN** the user reads up to 2
- **THEN** the read position stays 4 and `unread` is 0

#### Scenario: Sending reads the chat
- **GIVEN** a chat with two unread bot messages
- **WHEN** the user sends a message
- **THEN** that bot has `unread` 0 in the list

#### Scenario: Bad body or unknown chat
- **WHEN** the user reads with no `message_id`, or in a chat they do not have
- **THEN** the response is 400 with `bad_request`, or 404 with `bot_not_found`

### Requirement: REL-33 Bot status
The server SHALL remember when each bot last called get-updates, through any bot door. A bot is online while a get-updates call of it runs, and for `Relay:BotOnlineSeconds` (start 60) after its last call ended. `GET /api/bots/{bot id}/status` SHALL answer `{"online": bool, "last_seen": time or null}`. The last-seen time SHALL survive a restart (it may be up to `BotOnlineSeconds` older there).

#### Scenario: Polling now
- **WHEN** a bot's get-updates call is waiting
- **THEN** the status is `online` true

#### Scenario: Gone quiet
- **GIVEN** a bot's last get-updates call ended at 12:00
- **WHEN** a user asks at 12:02 with the setting at 60
- **THEN** the status is `online` false and `last_seen` 12:00

#### Scenario: Never polled
- **WHEN** a user asks for a bot that never called get-updates
- **THEN** the status is `online` false and `last_seen` null

#### Scenario: After a restart
- **GIVEN** a bot polled and the server restarted
- **WHEN** a user asks
- **THEN** `last_seen` is not null

### Requirement: REL-34 New bot messages are pushed
Every new message from a bot SHALL be passed to Notification as a push for its user (NTF-06), after it is saved, through the outbox (LIVE-04). Its title is the bot's name, its body the text or else the file name, its link `/@<bot username>` and its tag `bot-<bot id>`. Edits, deletes, pins and the user's own messages SHALL NOT be pushed. A push SHALL NOT slow the bot's send.

#### Scenario: Bot writes
- **WHEN** bot "Shop" (`@shopbot`) sends "Your order is ready" to a user
- **THEN** one push for that user is passed on with title "Shop", body "Your order is ready", link `/@shopbot`

#### Scenario: A file without caption
- **WHEN** the bot sends the file `menu.pdf` with no caption
- **THEN** the push body is `menu.pdf`

#### Scenario: Nothing else is pushed
- **WHEN** the user sends a message and the bot edits and pins its own message
- **THEN** no push is passed on

### Requirement: REL-35 Push subscriptions door
The Client API SHALL have `GET /api/push` → `{"key": the VAPID public key, or null when push is off}`, `POST /api/push/subscribe` with the browser's subscription JSON (`endpoint`, `keys.p256dh`, `keys.auth`), and `POST /api/push/unsubscribe` with `{"endpoint"}`. Subscribe answers 204, or 400 `bad_subscription` (NTF-04), or 503 `push_off`. Unsubscribe answers 204 and removes the endpoint only if it belongs to this user. These are Client API calls (REL-12, ADM-01, SEC-03).

#### Scenario: Turn on
- **GIVEN** push keys are set
- **WHEN** a user subscribes with a valid subscription
- **THEN** the answer is 204 and the subscription is stored for that user

#### Scenario: Push off
- **GIVEN** no push keys are set
- **WHEN** a user asks `GET /api/push` and then subscribes
- **THEN** `key` is null, and the subscribe answer is 503 with `push_off`

#### Scenario: Turn off
- **GIVEN** a user's stored subscription
- **WHEN** another user unsubscribes its endpoint, and then the owner does
- **THEN** it stays after the first call and is gone after the second
