## ADDED Requirements

### Requirement: AUTH-21 Make an access key
With a session, `POST /api/account/keys` with a `name` of 1 to 64 characters SHALL answer 201 with `id`, `name`, `key`, `created_at` and `expires_at`, else 400 `bad_request`. The key SHALL be `dalan_` and 43 base64url characters, shown only here; only its SHA-256 hash is stored. It expires after `Identity:AccessKeyDays` (start 90) days. At most `Identity:MaxAccessKeys` (start 10) per user, else 409 `key_limit`. The audit log gets `key.create`.

#### Scenario: Make a key
- **WHEN** a logged-in user makes a key named `laptop`
- **THEN** the answer is 201 with a key that matches `^dalan_[A-Za-z0-9_-]{43}$`, `expires_at` 90 days from now, and the audit log has `key.create`

#### Scenario: Only the hash is stored
- **WHEN** a key is made
- **THEN** no column of `identity.access_keys` contains the key, and the list shows the name but not the key

#### Scenario: Too many keys
- **GIVEN** `Identity:MaxAccessKeys` is 1 and the user has one key
- **WHEN** the user makes another
- **THEN** the answer is 409 `key_limit`

### Requirement: AUTH-23 List and delete access keys
With a session, `GET /api/account/keys` SHALL list the user's keys (`id`, `name`, `created_at`, `expires_at`) without the key. `DELETE /api/account/keys/{id}` SHALL delete one of the user's keys, else 404 `key_not_found`; the audit log gets `key.delete`. The three key calls SHALL NOT accept an access key.

#### Scenario: Delete
- **GIVEN** a user with a key
- **WHEN** the user deletes it
- **THEN** it is not in the list, a call with it gets 401, and the audit log has `key.delete`

#### Scenario: Another user's key
- **WHEN** user B deletes user A's key
- **THEN** the answer is 404 `key_not_found` and the key still works

#### Scenario: A key cannot make keys
- **WHEN** `POST /api/account/keys` is called with only an access key
- **THEN** the answer is 401

### Requirement: AUTH-22 Where an access key works
A call with `Authorization: Bearer <access key>` SHALL be served as the key's user only at the builder calls (`/api/builder/...`) and the MCP door (`/api/mcp`). Any other call with only a key SHALL get 401 `unauthorized`. An unknown, deleted or expired key, or a key of a suspended user, SHALL get 401 `unauthorized`. The session token SHALL be read only from its cookie, never from the `Authorization` header.

#### Scenario: Builder call
- **GIVEN** an approved builder's key
- **WHEN** `GET /api/builder/bots` is called with it
- **THEN** the answer is 200 with the builder's bots

#### Scenario: Not a builder door
- **WHEN** `GET /api/me` or `GET /api/chats` is called with only a key
- **THEN** the answer is 401

#### Scenario: Expired
- **GIVEN** a key made 91 days ago
- **WHEN** it is used
- **THEN** the answer is 401

#### Scenario: Suspended user
- **GIVEN** a builder's key
- **WHEN** an admin suspends the builder
- **THEN** the next call with the key gets 401

#### Scenario: Session token in the header
- **WHEN** a valid session token is sent as `Authorization: Bearer` with no cookie
- **THEN** the answer is 401
