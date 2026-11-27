## MODIFIED Requirements

### Requirement: CLI-55 Install a bot
On a bot's chat page (`/@username`) the page SHALL link `/@username/manifest.webmanifest`, and its `rel="icon"` and `rel="apple-touch-icon"` links SHALL be `/@username/icon` (REL-54); on every other screen it SHALL link the main manifest and Dalan's icons again. So the browser's Install, Chrome's "Create shortcut" and "Add to Home Screen" on a bot's page make an app of that bot with the bot's icon, and the app opens its chat.

#### Scenario: Bot page
- **WHEN** the chat of @mofidbot is shown
- **THEN** the manifest link is `/@mofidbot/manifest.webmanifest`, and both icon links are `/@mofidbot/icon`

#### Scenario: Back to the list
- **WHEN** the user goes from the chat to `/chats`
- **THEN** the manifest link is `/manifest.webmanifest`, the icon link is `/icons/icon.svg` and the apple-touch-icon link is `/icons/icon-192.png`
