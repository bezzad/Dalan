## ADDED Requirements

### Requirement: TG-26 A moved chat in Telegram's shape
The Telegram door SHALL show a `chat_moved` update (REL-56) as a `message` in the old chat with `migrate_to_chat_id` set to the new chat id, and a `chat_moved` error as 400 with `parameters.migrate_to_chat_id`, the way Telegram tells a bot that a group moved.

#### Scenario: Update
- **GIVEN** chat 9 of bot 5 moved to chat 4
- **WHEN** the bot calls `getUpdates` on the Telegram door
- **THEN** it gets a message with `chat.id` 9 and `migrate_to_chat_id` 4

#### Scenario: Error
- **WHEN** the bot calls `sendMessage` with `chat_id` 9 on the Telegram door
- **THEN** the answer is `ok` false, `error_code` 400, and `parameters.migrate_to_chat_id` 4
