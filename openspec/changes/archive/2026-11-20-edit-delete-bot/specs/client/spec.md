## MODIFIED Requirements

### Requirement: CLI-24 My bots
The "My bots" screen SHALL list the builder's bots (username, name, last seen, suspended) and have a form to create one. The bot list SHALL have a "Create bot" button for every user: it opens "My bots" for a builder or an admin, and the account screen (CLI-23) for anyone else. After a create or a new token the screen SHALL show the token once, with a copy button. Each bot SHALL have Rename (REL-48), New token and Delete (REL-49); New token and Delete SHALL ask first.

#### Scenario: Token shown once
- **WHEN** a builder creates a bot
- **THEN** the token is shown with the copy button, and after closing it the list shows the bot without the token
