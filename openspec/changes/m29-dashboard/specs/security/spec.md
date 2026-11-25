## ADDED Requirements

### Requirement: SEC-14 The server is the gate for admin pages
Hiding admin items in the client SHALL NOT be the only guard. Every `/api/admin` call SHALL be refused for a user who is not an admin, the same way for every admin call, and the try SHALL be in the access log. No answer to a common user SHALL hold admin-only data (other users' phones or emails, settings, logs).

#### Scenario: Common user calls an admin API
- **GIVEN** a logged-in user who is not an admin
- **WHEN** the user calls `GET /api/admin/settings` and `GET /api/admin/overview`
- **THEN** both are refused, and the access log has both calls

#### Scenario: Admins only from the server
- **WHEN** every API, the admin panel and the account merge are tried in the tests
- **THEN** none can make a user an admin; only `Identity:AdminPhones` in the server's environment does, and the panel cannot read or change it

#### Scenario: Nothing leaks in common answers
- **WHEN** the answers of every non-admin API a common user can call are read in the tests
- **THEN** none holds another user's phone or email, a setting value, or a log row

### Requirement: SEC-15 Second factor for admins
An admin (SEC-14: a phone in `Identity:AdminPhones`) SHALL have the second factor (AUTH-37) on. Every login of an admin, by password, SMS, email or GitHub, SHALL also need a TOTP code or an unused recovery code before a session is made. While an admin has no second factor yet, every `/api/admin` call SHALL be refused with 403 `admin_needs_2fa`, and the dashboard SHALL show only the page to set it up. An admin SHALL NOT be able to turn the second factor off.

#### Scenario: Admin logs in by SMS
- **GIVEN** an admin with the second factor on
- **WHEN** the admin enters the right SMS code
- **THEN** the answer asks for the TOTP code, and no session is made until a right code comes

#### Scenario: No second factor yet
- **GIVEN** an admin without the second factor
- **WHEN** the admin opens `/dashboard/admin` or calls `GET /api/admin/settings`
- **THEN** the page shows only the set-up of the second factor, and the call gets 403 `admin_needs_2fa`

#### Scenario: Cannot turn it off
- **WHEN** an admin asks to turn the second factor off
- **THEN** the answer is 403 and it stays on
