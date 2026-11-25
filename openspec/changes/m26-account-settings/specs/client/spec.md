## ADDED Requirements

### Requirement: CLI-77 Settings area
The account screen SHALL be a settings area with a side menu and one section per page, each at its own address: Profile (`/account/profile`), Login and security (`/account/security`), Notifications (`/account/notifications`), Preferences (`/account/preferences`), and, only for an approved builder or an admin, Builder (`/account/builder`) and Access keys (`/account/keys`). `/account` SHALL open Profile at 760px and wider, and the menu list below 760px. Each section page SHALL show a breadcrumb, a title, a one-line description and rows of title and help on the start side and the control on the end side. Login methods (password, phone, email, GitHub) SHALL each be a card with a "Connected" or "Not connected" badge and one action. Theme and language SHALL be radio cards that apply at once. A row's button SHALL be primary only while its value is changed.

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
