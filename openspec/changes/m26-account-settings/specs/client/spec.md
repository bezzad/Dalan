## ADDED Requirements

### Requirement: CLI-77 Settings area
The account screen SHALL be a settings area: a side menu, one section per page, each at its own address (`/account/profile`, `/account/security`, `/account/notifications`, `/account/preferences` and, only for an approved builder or an admin, `/account/builder` and `/account/keys`). `/account` SHALL open Profile at 760px and wider, and the menu list below. A section page SHALL show a breadcrumb, a title, a description and rows. A row's button SHALL be primary only while its value is changed.

#### Scenario: Desktop
- **WHEN** a user opens `/account` in a 1280px wide browser
- **THEN** the side menu shows the Account group, the current item is Profile, and the page shows `Settings › Profile` with the username, phone and email rows

#### Scenario: One address per section
- **WHEN** the user picks "Login and security", then presses Back
- **THEN** the address was `/account/security`, and Back shows Profile again

#### Scenario: Builder group
- **GIVEN** a user who is not an approved builder or an admin
- **WHEN** the settings area opens
- **THEN** there is no Building group, and `/account/keys` opens Profile

#### Scenario: Login methods
- **GIVEN** a user with a password and a phone, no email and no GitHub
- **WHEN** Login and security opens
- **THEN** Password and Phone show "Connected", Email and GitHub show "Not connected" with "Set up" and "Link"

#### Scenario: Preferences
- **WHEN** the user picks "Dark" or "English" in Preferences
- **THEN** the theme or the language applies at once, and that radio card is the chosen one

#### Scenario: Phone
- **WHEN** `/account` opens on a 360px phone
- **THEN** it shows the menu as a list of rows; picking Notifications opens `/account/notifications` with "‹ Settings" at the start

#### Scenario: Quiet buttons
- **WHEN** the Profile page opens
- **THEN** no button on it is primary; after the username is changed, its "Save" is primary

### Requirement: CLI-78 Grouped user menu
The user menu SHALL open with the user's name and, in grey, the phone or the email. Under it SHALL come groups with an icon per item and a line between groups: My bots, Chats, My apps; then Settings and, for an admin, Admin; then Log out alone.

#### Scenario: Admin
- **GIVEN** an admin with the email `a@example.com` and no phone
- **WHEN** the user menu opens
- **THEN** it shows the name, `a@example.com` in grey, then three groups: (My bots, Chats, My apps), (Settings, Admin), (Log out), each item with an icon

#### Scenario: Not an admin
- **WHEN** a user who is not an admin opens the menu
- **THEN** the second group has only Settings
