## MODIFIED Requirements

### Requirement: ADM-11 Users for the admin
`GET /api/admin/users?q=<text>` SHALL find users by exact id, phone (any form), email (any case), username or name start; with no `q`, every user. Newest first, `Admin:PageSize` a page, `before=<id>` the next. Each: id, phone, email, name, username, creation time, `builder`, `suspended`, `bots` (count) and `last_active_at`. `?builder=requested`: open requests, oldest first. `POST .../{id}/builder` `{"approve"}` (AUTH-19), `.../{id}/suspend` `{"suspended"}` (AUTH-20). Unknown id: 404 `user_not_found`.

#### Scenario: Find by phone
- **GIVEN** a user with phone `+989121234567`
- **WHEN** an admin searches `q=0912 123 4567`
- **THEN** that user is in the answer with the phone

#### Scenario: Find by email
- **GIVEN** a user with email `sara@example.com` and no phone
- **WHEN** an admin searches `q=SARA@example.com`
- **THEN** that user is in the answer with the email and no phone

#### Scenario: Everyone, a page at a time
- **GIVEN** three users and a page size of 2
- **WHEN** an admin lists with no `q`, then with `before` the last id of that page
- **THEN** the first page has the two newest users and the second the oldest

#### Scenario: Bot count
- **GIVEN** user 7 owns two bots
- **WHEN** an admin lists users
- **THEN** user 7 has `bots` 2

#### Scenario: Requests
- **GIVEN** two users asked to become builders
- **WHEN** an admin lists `builder=requested`
- **THEN** both are in the answer, oldest request first

#### Scenario: Not myself
- **WHEN** an admin suspends their own account
- **THEN** the answer is 400 `bad_request` and nothing changes

### Requirement: ADM-12 Bots for the admin
`GET /api/admin/bots?q=<text>` SHALL find bots by exact id, username or owner id; with no `q`, every bot. Newest first, `Admin:PageSize` a page, `before=<id>` the next. Each: id, username, name, owner id, creation time, last seen time, `suspended` and `users` (people who started its chat). `POST /api/admin/bots/{id}/suspend` with `{"suspended": true|false}` SHALL suspend or unsuspend it (REL-38). An unknown id SHALL get 404 `bot_not_found`.

#### Scenario: Find by owner
- **GIVEN** user 7 owns two bots
- **WHEN** an admin searches `q=7`
- **THEN** both bots are in the answer

#### Scenario: Everyone
- **GIVEN** three bots
- **WHEN** an admin lists with no `q`
- **THEN** all three are in the answer, the newest first, each with `users`

#### Scenario: Suspend
- **WHEN** an admin suspends bot 5
- **THEN** the answer shows `suspended` true and the next Bot API call of bot 5 gets 403 `bot_suspended`

## ADDED Requirements

### Requirement: ADM-20 One user or bot for the admin
`GET /api/admin/users/{id}` SHALL give `{"user", "bots"}`: the user as in ADM-11 and every bot they own as in ADM-12. `GET /api/admin/bots/{id}` SHALL give `{"bot", "owner"}`: the bot as in ADM-12 and its owner as in ADM-11. Unknown ids get 404 `user_not_found` or `bot_not_found`. Each call is in the access log, like every admin call.

#### Scenario: A user with bots
- **GIVEN** user 7 owns bots 5 and 6
- **WHEN** an admin asks `GET /api/admin/users/7`
- **THEN** the answer has user 7 and bots 5 and 6

#### Scenario: A bot and its owner
- **WHEN** an admin asks `GET /api/admin/bots/5`
- **THEN** the answer has bot 5 and user 7 as its owner
