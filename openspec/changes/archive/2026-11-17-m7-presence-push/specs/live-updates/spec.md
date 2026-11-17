## ADDED Requirements

### Requirement: LIVE-10 Read position on other devices
After a read (REL-32), every open stream of that user SHALL get an event named `read` with JSON data `bot_id` and `unread`. It is not stored: a lost one is fixed by the next list load.

#### Scenario: Read on the phone
- **GIVEN** a user with an open stream on a laptop
- **WHEN** the same user reads a chat on the phone and 1 bot message stays unread
- **THEN** the laptop's stream gets a `read` event with that bot's id and `unread` 1
