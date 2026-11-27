## MODIFIED Requirements

### Requirement: CLI-55 Install a bot
On a bot's chat (`/@username`) the page SHALL link `/@username/manifest.webmanifest`, its `rel="icon"` and `rel="apple-touch-icon"` SHALL be `/@username/icon` (REL-54) and its title the bot's name; elsewhere Dalan's. In a browser tab, going between a bot's chat and any other screen (or another bot) SHALL load the page anew, since Safari reads the manifest only at load. So Install and "Add to Home Screen" there have the bot's address, icon and name.

#### Scenario: Bot page
- **WHEN** the chat of @mofidbot is shown
- **THEN** the manifest link is `/@mofidbot/manifest.webmanifest`, both icon links are `/@mofidbot/icon`, and the title is the bot's name

#### Scenario: Back to the list
- **WHEN** the user goes from the chat to `/chats`
- **THEN** the manifest link is `/manifest.webmanifest`, the icon link is `/icons/icon.svg` and the apple-touch-icon link is `/icons/icon-192.png`

#### Scenario: Into a chat in a browser tab
- **GIVEN** a browser tab, not an installed app, on `/dashboard/chats`
- **WHEN** the user opens the chat of @mofidbot, or goes back from it
- **THEN** the page loads anew at the new address

#### Scenario: In an installed app
- **GIVEN** the installed app
- **WHEN** the user opens a chat
- **THEN** the page does not load anew
