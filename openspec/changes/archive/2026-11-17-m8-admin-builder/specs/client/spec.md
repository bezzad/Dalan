## ADDED Requirements

### Requirement: CLI-23 Builder request in the account screen
The account screen SHALL show the user's builder state: a button "become a builder" when it is `none`, "waiting for approval" when `requested`, and a button that opens "My bots" when `approved` or for an admin. Admins SHALL also see a button that opens the admin screen.

#### Scenario: What the account screen shows
- **WHEN** the builder state is `none`, `requested` or `approved`, or the user is an admin
- **THEN** the screen shows the request button, the waiting note, the "My bots" button, or the "My bots" and "Admin" buttons

### Requirement: CLI-24 My bots
The "My bots" screen SHALL list the builder's bots (username, name, last seen, suspended) and have a form to create one. After a create or a token rotation the screen SHALL show the token once, with a copy button and a note that it is shown only now. Rotation SHALL ask for confirmation first, because the old token stops at once.

#### Scenario: Token shown once
- **WHEN** a builder creates a bot
- **THEN** the token is shown with the copy button, and after closing it the list shows the bot without the token

### Requirement: CLI-25 Admin screen
The admin screen SHALL have tabs: settings (grouped by section, each with its value, an edit field, a reset button when changed, and an "after restart" mark), users (search; approve or refuse a builder request; suspend or unsuspend; open the user's access log), builder requests, bots (search; suspend or unsuspend; open the bot's access log), access log (search by user, bot, phone or address; times shown in UTC and local time) and audit log. A value is checked by its kind before it is sent.

#### Scenario: Settings grouped and checked
- **GIVEN** settings `Relay:MaxTextLength` (number) and `Admin:PageSize` (number)
- **WHEN** the settings tab is drawn and the admin types `abc` for a number
- **THEN** they are in the groups `Admin` and `Relay`, and the save button is off for `abc`

#### Scenario: Suspended bot in a chat
- **WHEN** a user writes to a suspended bot
- **THEN** the chat shows that the bot is suspended
