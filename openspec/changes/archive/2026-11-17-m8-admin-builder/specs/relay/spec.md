## ADDED Requirements

### Requirement: REL-36 A builder creates bots
`POST /api/builder/bots` with `{"username", "name"}` SHALL create a bot owned by the caller and answer 201 with `id`, `username`, `name` and the `token`, the only answer that holds it. Username: 5 to 32 of `a-z 0-9 _`, starting with a letter, made lower case, unique (409 `username_taken`); name: 1 to `Relay:MaxBotNameLength` (start 64); else 400 `bad_request`. At most `Relay:MaxBotsPerBuilder` (start 20) per owner (409 `bot_limit`). A non-builder (AUTH-19) gets 403 `not_builder`.

#### Scenario: Create
- **GIVEN** an approved builder
- **WHEN** the builder creates `@Weather_Bot` named "Weather"
- **THEN** the answer is 201 with username `weather_bot` and a token that works on the Bot API

#### Scenario: Not a builder
- **WHEN** a user who is not a builder creates a bot
- **THEN** the answer is 403 `not_builder` and no bot exists

#### Scenario: Taken
- **GIVEN** a bot `weather_bot` exists
- **WHEN** a builder creates `weather_bot`
- **THEN** the answer is 409 `username_taken`

#### Scenario: Too many
- **GIVEN** `Relay:MaxBotsPerBuilder` is 1 and the builder has one bot
- **WHEN** the builder creates another
- **THEN** the answer is 409 `bot_limit`

### Requirement: REL-39 My bots
`GET /api/builder/bots` SHALL list the caller's own bots (`id`, `username`, `name`, creation time, last seen time, `suspended`), never a token. The builder calls are Client API calls (REL-12, ADM-01, SEC-03).

#### Scenario: Only my bots
- **GIVEN** builder A has one bot and builder B another
- **WHEN** A lists the builder bots
- **THEN** only A's bot is in the answer, with no token

### Requirement: REL-37 Token rotation
`POST /api/builder/bots/{id}/token` SHALL give the bot a new token, answer it once, and make the old token stop at once. Only the owner (or an admin) SHALL do this; for any other bot the answer SHALL be 404 `bot_not_found`. The rotation is in the audit log (ADM-09) without the token.

#### Scenario: Old token stops
- **GIVEN** a builder's bot with token T1
- **WHEN** the builder rotates the token and gets T2
- **THEN** a Bot API call with T1 gets 401 and one with T2 works

#### Scenario: Another owner's bot
- **WHEN** builder B rotates the token of builder A's bot
- **THEN** the answer is 404 `bot_not_found` and A's token still works

### Requirement: REL-38 Suspended bot
A bot suspended by an admin SHALL get 403 `bot_suspended` on every core Bot API call with its valid token; the access log row SHALL still be written (ADM-01). A user who sends a message or a file to it, presses one of its buttons or starts it SHALL get 403 `bot_suspended`. `GET /api/bots/{id}` and the bot list SHALL say `suspended` true. After unsuspend the bot SHALL work again with the same token.

#### Scenario: Bot call
- **GIVEN** bot 5 is suspended
- **WHEN** bot 5 calls `GET /v1/updates` from `203.0.113.7`
- **THEN** the answer is 403 `bot_suspended` and the access log has the row

#### Scenario: User writes
- **GIVEN** a suspended bot
- **WHEN** a user sends it a text
- **THEN** the answer is 403 `bot_suspended` and nothing is stored

#### Scenario: Unsuspend
- **GIVEN** a suspended bot
- **WHEN** an admin unsuspends it
- **THEN** its next `GET /v1/updates` works
