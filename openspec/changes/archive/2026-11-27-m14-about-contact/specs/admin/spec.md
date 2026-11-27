# Admin

## ADDED Requirements

### Requirement: ADM-17 Contact messages
`POST /api/contact` SHALL take `{"email", "text"}` with or without a session and answer 201. A logged-in user with an email SHALL get that email; others SHALL send a valid one, or 400 `bad_email`. The trimmed text SHALL have 1 to `Admin:ContactMaxLength` (2000) characters, or 400 `bad_text`. One address SHALL send at most `Admin:ContactPerAddressPerDay` (5) in 24 hours, or 429 `too_many_messages`. The message SHALL keep the email, user id or none, real address, port and time.

#### Scenario: Visitor sends a question
- **WHEN** a visitor with no session from 203.0.113.9:40000 sends `{"email": "Ali@Example.com", "text": "  How much is it?  "}`
- **THEN** the answer is 201, and the admin sees one unread message with email `ali@example.com`, text `How much is it?`, no user id and address `203.0.113.9` port 40000

#### Scenario: Visitor without email
- **WHEN** a visitor sends `{"email": "", "text": "Hi"}`
- **THEN** the answer is 400 `bad_email`

#### Scenario: Logged-in user with an email
- **GIVEN** Sara logged in with `sara@example.com`
- **WHEN** she sends `{"email": "other@example.com", "text": "Hi"}`
- **THEN** the message has email `sara@example.com` and her user id, and her call is in the access log (ADM-01)

#### Scenario: Logged-in user with no email
- **GIVEN** Reza logged in by SMS only
- **WHEN** he sends `{"email": "reza@example.com", "text": "Hi"}`
- **THEN** the message has email `reza@example.com` and his user id

#### Scenario: Limits
- **GIVEN** `Admin:ContactMaxLength` is 10 and `Admin:ContactPerAddressPerDay` is 2
- **WHEN** one address sends an 11-character text, then three good messages
- **THEN** the first is 400 `bad_text`, the next two are 201 and the last is 429 `too_many_messages`

#### Scenario: Max length is public
- **WHEN** a visitor asks for the public settings
- **THEN** the answer has `contact_max_length` 2000

### Requirement: ADM-18 Admin reads contact messages
`GET /api/admin/contact` SHALL give an admin the contact messages newest first, a page at a time (`before`). `POST /api/admin/contact/{id}/read` with `{"read": true|false}` SHALL mark one message, and the audit log SHALL record it.

#### Scenario: Read mark
- **WHEN** the admin marks a message read
- **THEN** the list shows it read and the audit log has `contact.read`

#### Scenario: Only the admin reads
- **WHEN** a user who is not an admin asks for `/api/admin/contact`
- **THEN** the answer is 403 `not_admin`
