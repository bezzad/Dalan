## MODIFIED Requirements

### Requirement: CLI-24 My bots
The "My bots" screen SHALL list the builder's bots (username, name, last seen, suspended) and have a form to create one. The bot list SHALL have a "Create bot" button for every user: it opens "My bots" for a builder or an admin, and the account screen (CLI-23) for anyone else. After a create or a token rotation the screen SHALL show the token once, with a copy button and a note that it is shown only now. Rotation SHALL ask for confirmation first, because the old token stops at once.

#### Scenario: Token shown once
- **WHEN** a builder creates a bot
- **THEN** the token is shown with the copy button, and after closing it the list shows the bot without the token
