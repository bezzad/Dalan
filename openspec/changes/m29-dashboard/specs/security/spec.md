## ADDED Requirements

### Requirement: SEC-14 The server is the gate for admin pages
Hiding admin items in the client SHALL NOT be the only guard. Every `/api/admin` call SHALL be refused for a user who is not an admin, the same way for every admin call, and the try SHALL be in the access log. No answer to a common user SHALL hold admin-only data (other users' phones or emails, settings, logs).

#### Scenario: Common user calls an admin API
- **GIVEN** a logged-in user who is not an admin
- **WHEN** the user calls `GET /api/admin/settings` and `GET /api/admin/overview`
- **THEN** both are refused, and the access log has both calls

#### Scenario: Nothing leaks in common answers
- **WHEN** the answers of every non-admin API a common user can call are read in the tests
- **THEN** none holds another user's phone or email, a setting value, or a log row
