# Spec Delta

## MODIFIED Requirements

### Requirement: ADM-10 Search the access log
`GET /api/admin/access-log` with exactly one of `user=<id>`, `bot=<id>`, `phone=<number>`, `email=<address>` or `address=<ip>` SHALL list the matching access log rows (actor, actor id, phone, email, address, port, UTC time), newest first, `Admin:PageSize` at a time, with `before=<id>` for older rows. A phone in any Iranian form SHALL match; an email SHALL match without regard to letter case; an IPv4 address written as IPv6 SHALL match the IPv4 address. No filter, more than one, or a bad value SHALL get 400 `bad_request`.

#### Scenario: By bot
- **GIVEN** bot 5 called from `203.0.113.7` port `51000` and bot 6 from `198.51.100.1`
- **WHEN** an admin searches with `bot=5`
- **THEN** the answer has one row: bot 5, `203.0.113.7`, `51000` and its time

#### Scenario: By address
- **GIVEN** a user and a bot both called from `203.0.113.7`
- **WHEN** an admin searches with `address=::ffff:203.0.113.7`
- **THEN** both rows are in the answer

#### Scenario: By phone
- **GIVEN** a login code was sent to `+989121234567` (ADM-04)
- **WHEN** an admin searches with `phone=09121234567`
- **THEN** that row is in the answer

#### Scenario: By email
- **GIVEN** a login code was sent to `sara@example.com` (ADM-16)
- **WHEN** an admin searches with `email=Sara@Example.com`
- **THEN** that row is in the answer

#### Scenario: No filter
- **WHEN** an admin searches with no filter
- **THEN** the answer is 400 `bad_request`

### Requirement: ADM-11 Users for the admin
`GET /api/admin/users?q=<text>` SHALL find users by exact id, phone (any Iranian form), email (any letter case), username, or the start of the name, at most `Admin:PageSize`, each with id, phone, email, name, username, creation time, `builder` and `suspended`. `?builder=requested` SHALL list open requests, oldest first. `POST /api/admin/users/{id}/builder` `{"approve"}` approves or refuses (AUTH-19); `POST /api/admin/users/{id}/suspend` `{"suspended"}` (AUTH-20). Unknown id: 404 `user_not_found`.

#### Scenario: Find by phone
- **GIVEN** a user with phone `+989121234567`
- **WHEN** an admin searches `q=0912 123 4567`
- **THEN** that user is in the answer with the phone

#### Scenario: Find by email
- **GIVEN** a user with email `sara@example.com` and no phone
- **WHEN** an admin searches `q=SARA@example.com`
- **THEN** that user is in the answer with the email and no phone

#### Scenario: Requests
- **GIVEN** two users asked to become builders
- **WHEN** an admin lists `builder=requested`
- **THEN** both are in the answer, oldest request first

#### Scenario: Not myself
- **WHEN** an admin suspends their own account
- **THEN** the answer is 400 `bad_request` and nothing changes

## ADDED Requirements

### Requirement: ADM-16 Email code requests are recorded
For every login code that is sent by email the server SHALL keep an access log row (actor `email`) with the email address, the real client address (SEC-10) of the caller who asked for it, the source port when known, and the UTC time. Every sent email gets its own row. A request that sends no email writes no row. ADM-03 applies to these rows too.

#### Scenario: Code sent
- **WHEN** a caller at `203.0.113.7` port `51000` asks a login code for `sara@example.com` and the email is sent
- **THEN** one row holds that address, `203.0.113.7`, `51000` and the time of the call

#### Scenario: Refused request
- **WHEN** a caller asks a code too soon and gets 429 `email_wait`
- **THEN** no new row is written
