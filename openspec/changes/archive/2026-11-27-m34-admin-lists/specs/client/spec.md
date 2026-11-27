## MODIFIED Requirements

### Requirement: CLI-78 Grouped user menu
The user menu SHALL open with the user's name and, in grey, the phone or the email. Under it SHALL come groups with an icon per item and a line between groups: My bots, Chats, My apps; then Dashboard; then Log out alone. Dashboard SHALL open the admin overview for an admin and `/dashboard` for anyone else (owner, 2026-11-27: no separate Settings or Admin item; both are in the dashboard's side menu).

#### Scenario: Admin
- **GIVEN** an admin with the email `a@example.com` and no phone
- **WHEN** the user menu opens
- **THEN** it shows the name, `a@example.com` in grey, then three groups: (My bots, Chats, My apps), (Dashboard), (Log out), each item with an icon, and Dashboard opens `/dashboard/admin`

#### Scenario: Not an admin
- **WHEN** a user who is not an admin opens the menu
- **THEN** the second group has only Dashboard, which opens `/dashboard`

## ADDED Requirements

### Requirement: CLI-93 Admin tables and detail pages
Users and Bots in the admin dashboard SHALL open as tables of everyone, newest first, with "More" for the next page and a search box above that narrows them. Users: name and username, phone or email, builder state, bots, joined, last active, a suspended mark. Bots: name, username, owner, chat users, last seen, a suspended mark. A row SHALL open `/dashboard/admin/users/{id}` or `/dashboard/admin/bots/{id}`: the details, the actions, and the user's bots or the bot's owner as links.

#### Scenario: Users without a search
- **GIVEN** five users
- **WHEN** an admin opens Users
- **THEN** a table shows all five, the newest first, with no search typed

#### Scenario: A user's page
- **WHEN** an admin clicks the row of user 7, who owns two bots
- **THEN** the address is `/dashboard/admin/users/7`, the page shows the user's details and actions, and two bot rows that open their pages

#### Scenario: Not an admin
- **WHEN** a user who is not an admin opens `/dashboard/admin/users/7`
- **THEN** the page is "Not found"
