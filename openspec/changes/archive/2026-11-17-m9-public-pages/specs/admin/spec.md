## ADDED Requirements

### Requirement: ADM-13 Showcase review
`GET /api/admin/showcase` SHALL list the `pending` entries, oldest change first, at most `Admin:PageSize`, each with the bot's id, username, name, owner id, `suspended`, description, category, `icon` and change time. `POST /api/admin/showcase/{bot id}` with `{"approve": true|false}` SHALL set the entry to `listed` or `refused` and record `showcase.approve` or `showcase.refuse` in the audit log. A bot with no entry SHALL get 404 `showcase_not_found`.

#### Scenario: Approve
- **GIVEN** a pending entry for bot 5
- **WHEN** an admin approves it
- **THEN** bot 5 is in the public showcase and the audit log has `showcase.approve`

#### Scenario: Refuse
- **GIVEN** a pending entry for bot 5
- **WHEN** an admin refuses it
- **THEN** bot 5 is not in the public showcase, the builder's list shows `refused`, and the request is gone from the pending list

#### Scenario: Not an admin
- **WHEN** a user who is not an admin lists the pending entries
- **THEN** the answer is 403

### Requirement: ADM-14 Numbers for the public pages
`GET /api/public/settings` SHALL need no session and give the current values of `Relay:HistoryMessages`, `Relay:HistoryDays`, `Admin:AccessLogRetentionDays`, `Relay:FileRetentionDays` and `Relay:UpdateExpirySeconds` as `history_messages`, `history_days`, `access_log_days`, `file_days` and `update_expiry_seconds`. A change in the admin panel SHALL show in the next answer.

#### Scenario: Visitor reads the numbers
- **WHEN** a visitor with no session asks for the public settings
- **THEN** the answer has the five numbers, for example `access_log_days` 180

#### Scenario: Panel change
- **WHEN** an admin sets `Relay:HistoryDays` to 60
- **THEN** the next public answer has `history_days` 60
