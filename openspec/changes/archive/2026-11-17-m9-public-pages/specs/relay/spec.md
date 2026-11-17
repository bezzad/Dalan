## ADDED Requirements

### Requirement: REL-40 A builder asks to list a bot
`PUT /api/builder/bots/{id}/showcase` with `{"description", "category"}` SHALL create or change the bot's showcase entry and set it to `pending`. Description: 1 to `Relay:MaxBotDescriptionLength` (start 512) characters; category: one of `Relay:ShowcaseCategories`; else 400 `bad_request`. Another owner's bot SHALL get 404 `bot_not_found`. `GET /api/builder/bots` SHALL give each bot's `showcase` or null.

#### Scenario: Ask
- **GIVEN** a builder's bot that is not in the showcase
- **WHEN** the builder sends a description and the category `games`
- **THEN** the answer is 200 with state `pending`, the builder's list shows `state`, `description`, `category` and `icon`, and the audit log has `showcase.request`

#### Scenario: A change goes back to review
- **GIVEN** a listed entry
- **WHEN** the builder changes its description
- **THEN** the state is `pending` and the bot is no longer in the public list

#### Scenario: Unknown category
- **WHEN** the builder sends the category `casino`
- **THEN** the answer is 400 `bad_request`

#### Scenario: Another owner's bot
- **WHEN** builder B asks to list builder A's bot
- **THEN** the answer is 404 `bot_not_found` and no entry exists

### Requirement: REL-41 Showcase icon
`PUT /api/builder/bots/{id}/showcase/icon` SHALL take the raw image as the body and store it on the entry, setting it to `pending`. It SHALL accept only PNG, JPEG or WebP, known by the first bytes of the body, of at most `Relay:MaxBotIconKb` (start 100) KB; else 400 `bad_icon`. A bot with no entry SHALL get 404 `showcase_not_found`. `GET` on the same address SHALL give the icon to the owner or an admin.

#### Scenario: PNG icon
- **GIVEN** a bot with a showcase entry
- **WHEN** the builder sends a PNG image
- **THEN** the entry has an icon and is `pending`, and `GET` gives the same bytes as `image/png`

#### Scenario: Not an image
- **WHEN** the builder sends an SVG file or a text
- **THEN** the answer is 400 `bad_icon`

#### Scenario: Too big
- **GIVEN** `Relay:MaxBotIconKb` is 1
- **WHEN** the builder sends a 2 KB PNG
- **THEN** the answer is 400 `bad_icon`

### Requirement: REL-42 Leave the showcase
`DELETE /api/builder/bots/{id}/showcase` SHALL delete the entry at once, with no review. It is in the audit log (`showcase.leave`).

#### Scenario: Leave
- **GIVEN** a listed bot
- **WHEN** its builder deletes the entry
- **THEN** the bot is not in the public list and its icon gets 404

### Requirement: REL-43 Public showcase
`GET /api/showcase` SHALL need no session and give `categories` and `bots`: only bots whose entry is `listed` and that are not suspended, in rising id order, at most `Relay:ShowcasePageSize` (start 50). `category=<key>` keeps one category; `after=<id>` gives the next page. `GET /api/showcase/{id}/icon` SHALL need no session and give the icon of a listed, not suspended bot, else 404 `not_found`.

#### Scenario: Only listed bots
- **GIVEN** bot A is listed, bot B is pending, bot C was refused and bot D has no entry
- **WHEN** a visitor with no session asks for the showcase
- **THEN** only bot A is in the answer, with `id`, `username`, `name`, `description`, `category` and `icon` (true or false)

#### Scenario: Suspended bot
- **GIVEN** a listed bot that the admin suspends
- **WHEN** a visitor asks for the showcase and the bot's icon
- **THEN** the bot is not in the list and the icon gets 404

#### Scenario: One category, next page
- **GIVEN** `Relay:ShowcasePageSize` is 1 and two listed games and one listed tool
- **WHEN** a visitor asks with `category=games`, then with `after` the first answer's id
- **THEN** each answer has one game, and no tool

### Requirement: REL-44 History kept per chat
A sweeper SHALL delete, every `Relay:HistorySweepMinutes` (start 60), the messages of each chat that are older than `Relay:HistoryDays` (start 30) days or are not among its newest `Relay:HistoryMessages` (start 200). A user message that still waits for the bot SHALL NOT be deleted. Sequence numbers and versions SHALL keep counting from where they were.

#### Scenario: Only the newest stay
- **GIVEN** `Relay:HistoryMessages` is 3 and a chat with messages 1 to 5
- **WHEN** the sweeper runs
- **THEN** the history has messages 3, 4 and 5, and the next message gets sequence number 6

#### Scenario: Old messages go
- **GIVEN** `Relay:HistoryDays` is 30 and a chat with a message 31 days old and one from today
- **WHEN** the sweeper runs
- **THEN** only the message from today is left

#### Scenario: A waiting message stays
- **GIVEN** `Relay:HistoryMessages` is 1 and a chat whose older message is a user message the bot has not read
- **WHEN** the sweeper runs
- **THEN** that message is still there
