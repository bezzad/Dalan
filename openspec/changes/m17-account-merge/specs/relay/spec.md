## ADDED Requirements

### Requirement: REL-55 Chats follow the merge
On `UserMerged` (AUTH-34) every chat, file and push subscription of the gone user SHALL move to the kept user. A chat only the gone user has SHALL keep its message ids. Two chats with the same bot SHALL become one chat, its messages in time order with new ids after the largest old id of both, so an old id never points to another message. The kept user's clients SHALL load the chat again. No bot SHALL change owner.

#### Scenario: Only the gone user had the chat
- **GIVEN** user 9 has a chat with bot 5 with messages 1 to 3, and user 4 has none
- **WHEN** user 9 is merged into user 4
- **THEN** user 4 has the chat with bot 5 with messages 1 to 3

#### Scenario: Both had a chat
- **GIVEN** user 4 has messages 1 to 3 and user 9 has messages 1 to 2 with bot 5, sent at different times
- **WHEN** user 9 is merged into user 4
- **THEN** user 4 has one chat with 5 messages in time order, with ids 4 to 8, ties going to user 4 first and then by old id

#### Scenario: Read position
- **GIVEN** user 4 had read up to its message 3, which gets id 7
- **WHEN** the chats are joined
- **THEN** user 4's read position is 7

#### Scenario: Push subscriptions
- **GIVEN** the two users have more push subscriptions together than `MaxSubscriptionsPerUser`
- **WHEN** user 9 is merged into user 4
- **THEN** user 4 keeps the newest `MaxSubscriptionsPerUser` of them

### Requirement: REL-56 The bot is told the chat moved
For each bot that had a chat with the gone user, the bot SHALL get one update with `chat_moved`: `from_chat_id` (the gone user), `to_chat_id` (the kept user) and `date`. For `Relay:MovedChatDays` (start 180) after the merge, a send to the old `chat_id` SHALL get 400 `chat_moved` with `to_chat_id`. Afterwards it SHALL get "chat not found".

#### Scenario: The update
- **GIVEN** user 9 had a chat with bot 5
- **WHEN** user 9 is merged into user 4
- **THEN** bot 5 gets an update with `chat_moved` from 9 to 4

#### Scenario: Send to the old chat
- **WHEN** bot 5 sends a message to chat 9 after the merge
- **THEN** the answer is 400 `chat_moved` with `to_chat_id` 4
