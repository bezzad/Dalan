## ADDED Requirements

### Requirement: CLI-70 Link and merge in the account screen
The account screen SHALL have a card to add a phone (CLI-34), add an email and link GitHub, each only while that way is empty. On `merge_needed` or `/account?merge=1` it SHALL show the merge question from `GET /api/account/merge`, with "Merge" and "Cancel". A refusal SHALL show its own text and no "Merge" button. All texts SHALL exist in Persian and in English.

#### Scenario: The question
- **GIVEN** an email user posts a phone of another account with the right code
- **WHEN** the answer is 409 `merge_needed`
- **THEN** the screen says "This phone already has an account. Merge it into this account?", which account is kept, the bots, sites and chats of each side, and that bots that chatted with the other account get its new chat id

#### Scenario: Cancel
- **WHEN** the person presses "Cancel"
- **THEN** the client sends `DELETE /api/account/merge` and the question closes

#### Scenario: Refused
- **GIVEN** the pending merge is refused with `both_have_products` or `merge_conflict`
- **WHEN** the question shows
- **THEN** it explains why and has no "Merge" button
