## ADDED Requirements

### Requirement: REL-48 Rename a bot
`PATCH /api/builder/bots/{id}` with `{"name":"..."}` SHALL change the bot's display name (1 to `Relay:MaxBotNameLength` characters, trimmed) and answer the bot like the list. The username SHALL NOT change. Only the owner (or an admin); for any other bot 404 `bot_not_found`. A bad name gets 400 `bad_request`. The change is in the audit log (ADM-09).

#### Scenario: New name, same username
- **WHEN** the owner renames bot `weather_bot` to "Arcane"
- **THEN** the answer has name "Arcane" and username `weather_bot`

### Requirement: REL-49 Delete a bot
`DELETE /api/builder/bots/{id}` SHALL delete the bot at once for its owner and its users: its token stops, it leaves every list and the showcase, and users can no longer write to it. Its chats and messages SHALL stay for the admin until the normal history expiry (owner, 2026-11-20). The username SHALL be free for a new bot; chats belong to the bot id, never the username, so a new bot with the same username never shows the old chats. Only the owner (or an admin); for any other bot 404 `bot_not_found`. The delete is in the audit log (ADM-09).

#### Scenario: Gone, chats kept, and the name is free
- **GIVEN** a builder's bot `weather_bot` with a chat
- **WHEN** the builder deletes it
- **THEN** its token gets 401, it is in no list, its messages stay in the database, and a new bot may take `weather_bot` with a new id and no old chats

