## MODIFIED Requirements

### Requirement: CLI-81 One dashboard for signed-in pages
Every signed-in page except a bot's chat SHALL be in one dashboard frame at `/dashboard/...` with a side menu in groups: Workspace (Chats; My bots and My apps for a builder or an admin), Account (the sections of CLI-77), and Admin only for an admin. The old addresses (`/account/*`, `/chats`, `/bots`, `/apps`, `/admin`) SHALL redirect to their new place. An admin page opened by a user who is not an admin SHALL show "Not found".

#### Scenario: Common user
- **GIVEN** a user who is not a builder or an admin
- **WHEN** the dashboard opens
- **THEN** the menu has Workspace with Chats only, and Account; there is no Admin group

#### Scenario: Admin
- **GIVEN** an admin
- **WHEN** the dashboard opens
- **THEN** the menu also has the Admin group with its nine items: Overview, Requests, Users, Bots, Showcase, Messages, Settings, Access log, Audit log

#### Scenario: Typed admin address
- **GIVEN** a user who is not an admin
- **WHEN** the user opens `/dashboard/admin/settings`
- **THEN** the page says "Not found" and shows no admin data

#### Scenario: Old address
- **WHEN** a user opens `/admin` or `/bots`
- **THEN** the address becomes `/dashboard/admin` or `/dashboard/bots`

#### Scenario: The menu takes every click
- **GIVEN** a wide window and a workspace page with a list (My bots with two bots)
- **WHEN** the user moves the pointer over a side menu item and clicks it
- **THEN** the item is the top element there, and its page opens
