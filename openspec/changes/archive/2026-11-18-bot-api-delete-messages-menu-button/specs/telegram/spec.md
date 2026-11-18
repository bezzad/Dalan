# Spec Delta

## MODIFIED Requirements

### Requirement: TG-09 Other methods get a clear error
A method the adapter does not serve SHALL get 400 with `{"ok":false,"error_code":400,"description":"Bad Request: method <name> is not supported by Dalan"}`, and SHALL do nothing. `deleteWebhook` (no webhook to delete), `sendChatAction`, and `setChatMenuButton` with no `menu_button` or type `commands` or `default` SHALL answer `true` and do nothing. A `web_app` menu button SHALL get 400 "Bad Request: web_app menu button is not supported by Dalan".

#### Scenario: Method Dalan does not have yet
- **WHEN** a bot calls `sendSticker`, `setWebhook` or `noSuchMethod`
- **THEN** the response is 400 and the `description` names the method and says "not supported by Dalan"

#### Scenario: Start of polling
- **WHEN** a bot calls `deleteWebhook`
- **THEN** the response is 200 with `"result":true`

#### Scenario: Typing
- **WHEN** a bot calls `sendChatAction` with `chat_id` and `action` "typing"
- **THEN** the response is 200 with `"result":true`

#### Scenario: Commands menu button
- **WHEN** a bot calls `setChatMenuButton` with `menu_button` `{"type":"commands"}`
- **THEN** the response is 200 with `"result":true`

#### Scenario: Mini App menu button
- **WHEN** a bot calls `setChatMenuButton` with `menu_button` of type `web_app`
- **THEN** the response is 400 and the `description` says "not supported by Dalan"

### Requirement: TG-17 Delete a message
`deleteMessage` (`chat_id`, `message_id`) SHALL delete the message (REL-24) and return `true`; a missing one SHALL get 400 "Bad Request: message to delete not found". `deleteMessages` (`chat_id`, `message_ids`: a JSON list of 1 to 100 ids) SHALL delete each message it finds, skip the missing ones, and return `true`; a list that is missing, empty, longer than 100 or not numbers SHALL get 400 and delete nothing.

#### Scenario: Delete
- **WHEN** the bot deletes message 2
- **THEN** the result is `true` and message 2 is marked deleted

#### Scenario: Delete several
- **GIVEN** the bot sent messages 2 and 3
- **WHEN** the bot calls `deleteMessages` with `message_ids` `[2, 3, 99]`
- **THEN** the result is `true` and messages 2 and 3 are marked deleted

#### Scenario: Bad list
- **WHEN** the bot calls `deleteMessages` with `message_ids` `[]`
- **THEN** the response is 400
