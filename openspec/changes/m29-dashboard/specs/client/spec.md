## ADDED Requirements

### Requirement: CLI-81 One dashboard for signed-in pages
Every signed-in page except a bot's chat SHALL be in one dashboard frame at `/dashboard/...` with a side menu in groups: Workspace (Chats; My bots and My apps for a builder or an admin), Account (the sections of CLI-77), and Admin (Overview, Requests, Users, Bots, Showcase, Messages, Settings, Access log, Audit log) only for an admin. The old addresses (`/account/*`, `/chats`, `/bots`, `/apps`, `/admin`) SHALL redirect to their new place. An admin page opened by a user who is not an admin SHALL show "Not found".

#### Scenario: Common user
- **GIVEN** a user who is not a builder or an admin
- **WHEN** the dashboard opens
- **THEN** the menu has Workspace with Chats only, and Account; there is no Admin group

#### Scenario: Admin
- **GIVEN** an admin
- **WHEN** the dashboard opens
- **THEN** the menu also has the Admin group with its nine items

#### Scenario: Typed admin address
- **GIVEN** a user who is not an admin
- **WHEN** the user opens `/dashboard/admin/settings`
- **THEN** the page says "Not found" and shows no admin data

#### Scenario: Old address
- **WHEN** a user opens `/admin` or `/bots`
- **THEN** the address becomes `/dashboard/admin` or `/dashboard/bots`

### Requirement: CLI-82 Admin overview
The first admin page SHALL show cards with counts, each a link to its page: users (and new in the last 7 days), bots (and online now), builder and showcase requests waiting, unread support messages, sites online.

#### Scenario: Waiting requests
- **GIVEN** two builder requests and one showcase request wait
- **WHEN** an admin opens the overview
- **THEN** the requests card says 3 and opens Requests

### Requirement: CLI-83 Readable admin settings
The admin settings page SHALL group the settings by service and show each as a setting row: its name and help in the page language (its key in small grey text), the control by kind (a number with its unit and range, a switch, a text), a "Changed" badge with the start value and "Reset" when it differs, and a "Needs restart" badge when it applies after a restart. A row's Save SHALL be primary only when its value changed. A search box SHALL filter rows by name, key or help. A risky setting SHALL ask with the old and new value before it saves.

#### Scenario: Readable row
- **WHEN** the Admin group of settings is shown in English
- **THEN** `AccessLogRetentionDays` shows as "Access log kept for" with "days", its help, and its key in grey

#### Scenario: Search
- **WHEN** the admin types "log" in the search box
- **THEN** only rows whose name, key or help has "log" stay

#### Scenario: Risky
- **WHEN** the admin changes the Bot API address and presses Save
- **THEN** a question shows the old and the new value, and nothing saves until the admin confirms
