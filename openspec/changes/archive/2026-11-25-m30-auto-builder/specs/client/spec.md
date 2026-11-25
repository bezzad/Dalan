## MODIFIED Requirements

### Requirement: CLI-23 Builder request in the account screen
The account screen SHALL show the user's builder state: a button "become a builder" when it is `none` or `refused` (with a note that the admin refused, for `refused`), "waiting for approval" when `requested`, and a button that opens "My bots" when `approved` or for an admin. A user with no phone SHALL see, instead of the request button, a note that building needs a verified mobile number (CLI-34). Admins SHALL also see a button that opens the admin screen.

#### Scenario: What the account screen shows
- **WHEN** the builder state is `none`, `requested` or `approved`, or the user is an admin
- **THEN** the screen shows the request button, the waiting note, the "My bots" button, or the "My bots" and "Admin" buttons

#### Scenario: No phone
- **WHEN** a user with no phone and builder state `none` opens the account screen
- **THEN** the screen shows the "verify your mobile first" note and no request button

## ADDED Requirements

### Requirement: CLI-84 A refused builder
The account screen SHALL show a refused user the note that the admin refused, with the request button. The admin screen SHALL show the state `refused` and an "approve" button for it. The texts SHALL exist in Persian and in English.

#### Scenario: Account screen
- **WHEN** a user in state `refused` opens the account screen
- **THEN** the screen shows the refused note and the request button

#### Scenario: Admin screen
- **WHEN** the admin opens a refused user
- **THEN** the screen says `refused` and has an "approve" button
