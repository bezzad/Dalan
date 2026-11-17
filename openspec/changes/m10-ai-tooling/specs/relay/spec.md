## ADDED Requirements

### Requirement: REL-45 Bot status for its builder
`GET /api/builder/bots/{id}` SHALL give the owner, or an admin, one bot: `id`, `username`, `name`, `created_at`, `online`, `last_seen`, `suspended` and `showcase`, with `online` by the same rule as PRE-01. The list (`GET /api/builder/bots`) SHALL give `online` for each bot too. Another user's bot or an unknown id SHALL get 404 `bot_not_found`.

#### Scenario: Online bot
- **GIVEN** a builder's bot that polled a moment ago
- **WHEN** the builder asks for its status
- **THEN** `online` is true and `last_seen` is set

#### Scenario: Never polled
- **WHEN** the builder asks for a new bot's status
- **THEN** `online` is false and `last_seen` is null

#### Scenario: Another owner's bot
- **WHEN** builder B asks for builder A's bot
- **THEN** the answer is 404 `bot_not_found`

### Requirement: REL-46 MCP door
`/api/mcp` SHALL be a stateless Streamable HTTP MCP server (protocol 2026-07-28 and 2025-11-25) with the tools `list_bots`, `create_bot` (`username`, `name`), `rotate_token` (`bot_id`) and `bot_status` (`bot_id`). Each tool SHALL follow the rules and give the JSON of its REST call (REL-36, REL-37, REL-39, REL-45). A refused call SHALL be a tool error with the same message.

#### Scenario: Tools
- **WHEN** an MCP client with a builder's key lists the tools
- **THEN** it gets `list_bots`, `create_bot`, `rotate_token` and `bot_status`

#### Scenario: Create and use
- **WHEN** the client calls `create_bot` with `weather_bot` and `Weather`
- **THEN** the result has the bot and its token, the token works at `GET /v1/updates`, and the audit log has `bot.create`

#### Scenario: Refused call
- **GIVEN** a bot named `weather_bot`
- **WHEN** the client calls `create_bot` with `weather_bot` again
- **THEN** the result is a tool error that says another bot has this username

### Requirement: REL-47 Who may call the MCP door
The MCP door SHALL take an access key or a session (AUTH-22), SHALL refuse a user who is not a builder with 403 `not_builder`, and SHALL write the access log (ADM-01) and count the per-user limit (SEC-03) on every call.

#### Scenario: No key
- **WHEN** an MCP client calls with no key and no session
- **THEN** the answer is 401

#### Scenario: Not a builder
- **WHEN** an MCP client calls with the key of a user who is not a builder
- **THEN** the answer is 403 `not_builder`

#### Scenario: Access log
- **WHEN** a builder calls the MCP door with a key
- **THEN** `admin.access_log` has a row for the user with the caller's address and port
