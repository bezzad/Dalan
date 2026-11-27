## ADDED Requirements

### Requirement: AUTH-35 Sessions
With a session, `GET /api/account/sessions` SHALL list the user's live sessions: id, browser and system (from the user agent), address, created, last used, and `current` for the caller's own. `DELETE /api/account/sessions/{id}` SHALL end one; `DELETE /api/account/sessions` SHALL end all but the caller's. An ended session's refresh SHALL fail. Each end SHALL be in the audit log.

#### Scenario: List
- **GIVEN** the user is logged in on a phone and a laptop
- **WHEN** the laptop asks for the list
- **THEN** it has two sessions, and only the laptop's has `current` true

#### Scenario: Log out the others
- **WHEN** the laptop ends all other sessions
- **THEN** the phone's next refresh fails with 401, and the laptop stays logged in

### Requirement: AUTH-36 Delete my account
With a session, `POST /api/account/delete` with the password (or, for a user with no password, a fresh SMS or email code) and the confirm word (one of `Identity:DeleteConfirmWords`) SHALL delete the account: profile, ways to log in, keys and sessions. It SHALL be refused with 409 `owns_things` while the user owns a bot or a site, and with 400 for a wrong proof or word. The access log and the sent-code records SHALL stay for their retention time. The delete SHALL be in the audit log.

#### Scenario: Owns a bot
- **GIVEN** the user owns one bot
- **WHEN** the user asks to delete the account
- **THEN** the answer is 409 `owns_things` and nothing is deleted

#### Scenario: No password
- **GIVEN** a user with no password
- **WHEN** the user asks `POST /api/account/delete/challenge`, solves it and calls `POST /api/account/delete/code`
- **THEN** a code goes to the account's own phone (else its email), and that code with the word deletes the account

#### Scenario: Admin
- **WHEN** an admin asks to delete the account
- **THEN** the answer is 403 `admin_delete`: admins come only from the server's environment

#### Scenario: Deleted
- **GIVEN** the user owns no bot or site
- **WHEN** the user sends the right password and word
- **THEN** the account is gone, its sessions fail, its access log rows stay, and the same phone can sign up again

### Requirement: AUTH-38 Address limits on access keys
An access key MAY have up to `Identity:MaxKeyAddresses` addresses or ranges (IPv4 or IPv6, CIDR). A call with that key from another address SHALL get 403 `key_address_refused`, recorded in the access log. A key with no list SHALL work from any address, as today.

#### Scenario: Set the list
- **WHEN** the owner sends `PUT /api/account/keys/{id}/addresses` with `["80.244.7.0/24", "2a01::1"]`
- **THEN** the key lists them; a bad address gets 400 `bad_address`, more than the limit 400 `too_many_addresses`

#### Scenario: Refused
- **GIVEN** a key limited to `80.244.7.0/24`
- **WHEN** a call with it comes from `5.6.7.8`
- **THEN** the answer is 403 `key_address_refused`, and the access log has the call
