# admin Specification

## Purpose
What the platform keeps and offers its owner: the access log that answers legal complaints, the admin door, the settings the admin changes, the audit log, and the user and bot lists.

## Requirements

### Requirement: ADM-01 Access log
The server SHALL record who called from where, so the owner can answer a legal complaint. For every Bot API call with a valid token and every Client API call with a valid session it SHALL keep the caller (bot or user, with its id), the real client address (SEC-10), the source port when the proxy reported it, and the UTC time. A call SHALL NOT be served when its row cannot be written. A call without a known caller writes no row.

#### Scenario: Bot call
- **WHEN** a bot calls the Bot API from `203.0.113.7` port `51000`
- **THEN** one row holds this bot, `203.0.113.7`, `51000` and the time of the call

#### Scenario: User call
- **WHEN** a logged-in user calls the Client API from `198.51.100.9`
- **THEN** one row holds this user, `198.51.100.9` and the time of the call

#### Scenario: Wrong token
- **WHEN** a call arrives with a wrong bot token
- **THEN** no row is written

#### Scenario: No port
- **WHEN** the proxy reports an address without a port
- **THEN** the row has no port

#### Scenario: IPv4 written as IPv6
- **WHEN** the address arrives as `::ffff:203.0.113.7`
- **THEN** the row holds `203.0.113.7`

#### Scenario: The row cannot be written
- **GIVEN** the access log table cannot be written
- **WHEN** a bot calls the Bot API
- **THEN** the call fails, and the next call tries to write the row again

### Requirement: ADM-02 One row per caller and address in an interval
The access log SHALL hold at most one row for the same caller and address in each span of `Admin:AccessLogIntervalMinutes` (start: 60). A call from a new address SHALL be written at once.

#### Scenario: Same address inside the interval
- **GIVEN** a bot's call was recorded
- **WHEN** the bot calls again from the same address 59 minutes later
- **THEN** there is still one row

#### Scenario: Same address after the interval
- **GIVEN** a bot's call was recorded
- **WHEN** the bot calls again from the same address 60 minutes later
- **THEN** there are two rows

#### Scenario: New address
- **GIVEN** a bot's call was recorded
- **WHEN** the bot calls from another address one second later
- **THEN** there are two rows

### Requirement: ADM-03 Access log retention
The server SHALL delete access log rows older than `Admin:AccessLogRetentionDays` (start: 180) in a sweep that runs every `Admin:AccessLogSweepMinutes` (start: 60), and SHALL NOT delete a younger row.

#### Scenario: Old and young rows
- **GIVEN** one row of 181 days ago and one of 179 days ago
- **WHEN** the sweep runs
- **THEN** only the younger row is left

### Requirement: ADM-04 SMS code requests are recorded
For every login code that is sent by SMS the server SHALL keep an access log row with the phone number, the real client address (SEC-10) of the caller who asked for it, the source port when known, and the UTC time. Every sent SMS gets its own row. A request that sends no SMS writes no row. ADM-03 applies to these rows too.

#### Scenario: Code sent
- **WHEN** a caller at `203.0.113.7` port `51000` asks a login code for a number and the SMS is sent
- **THEN** one row holds that number, `203.0.113.7`, `51000` and the time of the call

#### Scenario: Two codes
- **WHEN** two codes are sent to the same number from the same address
- **THEN** there are two rows

#### Scenario: Refused request
- **WHEN** a caller asks a code too soon and gets 429 `sms_wait`
- **THEN** no new row is written

### Requirement: ADM-05 Admin door
Every call under `/api/admin/` SHALL need a session (AUTH-03) of a user who is an admin (AUTH-18). Without a session the answer SHALL be 401 `unauthorized`; with the session of another user 403 `not_admin`, and the call SHALL do nothing. Every admin call SHALL be recorded in the access log (ADM-01).

#### Scenario: No session
- **WHEN** a call to `GET /api/admin/settings` has no session
- **THEN** the answer is 401 `unauthorized`

#### Scenario: Not an admin
- **WHEN** a logged-in user whose phone is not in `Identity:AdminPhones` calls `GET /api/admin/settings`
- **THEN** the answer is 403 `not_admin`

#### Scenario: Admin
- **WHEN** an admin calls `GET /api/admin/settings` from `203.0.113.7`
- **THEN** the answer is 200 and the access log has a row for this user and `203.0.113.7`

### Requirement: ADM-06 List the settings
`GET /api/admin/settings` SHALL list every editable setting with `key` (like `Relay:MaxTextLength`), `value`, `start` (from the file and environment), `changed`, `kind` (`number`, `switch`, `text`) and `restart` (read only at start). Editable SHALL be every leaf under `Admin`, `Identity`, `Relay` and `Notification`, plus `Dalan:IpRequestLimit` and `Dalan:IpLimitWindowSeconds`. Secrets and start-only safety settings SHALL NOT be listed (the list is in design.md, decision 4).

#### Scenario: Limits are listed
- **WHEN** an admin lists the settings
- **THEN** the list holds `Admin:AccessLogRetentionDays` with value `180`, kind `number`, and `Relay:BotOnlineSeconds`, `Relay:ClientStatusRefreshSeconds`, `Relay:LatestCommandWins` (kind `switch`) and `Notification:Push:AllowedHosts` (kind `text`)

#### Scenario: Secrets are never listed
- **GIVEN** `Notification:SmsIr:ApiKey` is set
- **WHEN** an admin lists the settings
- **THEN** no entry has the key, `Identity:SigningKey`, `Notification:Push:PrivateKey`, `Dalan:TrustedProxies` or `Identity:AdminPhones`, and no value in the answer is the API key

#### Scenario: Start-only setting
- **WHEN** an admin lists the settings
- **THEN** `Notification:Push:Workers` has `restart` true and `Relay:MaxTextLength` has `restart` false

### Requirement: ADM-07 Change a setting
`PUT /api/admin/settings/{key}` with `{"value"}` SHALL store the value in `admin.settings` and answer the entry. A stored value SHALL win over the file and environment, SHALL work without a restart (except `restart` settings) and SHALL hold after a restart. A number is a whole number from 1 to 2147483647, a switch `true` or `false`, a text at most `Admin:MaxSettingLength` (start 1000) characters; else 400 `bad_value`. A key not listed in ADM-06 SHALL get 404 `setting_not_found`.

#### Scenario: Takes effect at once
- **GIVEN** `Relay:MaxTextLength` is 4096
- **WHEN** an admin sets it to `10` and a user then sends a text of 11 characters
- **THEN** the send is refused with `text_too_long`

#### Scenario: Retention
- **WHEN** an admin sets `Admin:AccessLogRetentionDays` to `200` and the sweep runs
- **THEN** a row of 190 days ago is kept

#### Scenario: Survives a restart
- **GIVEN** an admin set `Relay:MaxTextLength` to `10`
- **WHEN** the server starts again
- **THEN** the setting is `10` before the first call is served

#### Scenario: Bad value
- **WHEN** an admin sets `Relay:MaxTextLength` to `0`, to `abc`, or `Relay:LatestCommandWins` to `yes`
- **THEN** the answer is 400 `bad_value` and the setting is unchanged

#### Scenario: Secret key
- **WHEN** an admin sets `Identity:SigningKey`
- **THEN** the answer is 404 `setting_not_found` and nothing is stored

### Requirement: ADM-08 Reset a setting
`DELETE /api/admin/settings/{key}` SHALL remove the stored value, so the start value holds again without a restart, and answer the entry. A key that is not listed SHALL get 404 `setting_not_found`.

#### Scenario: Reset
- **GIVEN** an admin set `Relay:MaxTextLength` to `10`
- **WHEN** the admin resets it
- **THEN** its value is `4096`, `changed` is false, and a text of 11 characters can be sent

### Requirement: ADM-09 Audit log
The server SHALL keep a row in `admin.audit_log` for every admin change (setting set or reset with old and new value, builder approved or refused, user or bot suspended or unsuspended) and for every bot created or token rotated. A row SHALL hold the user id, action, target, detail and UTC time, and never a token. `GET /api/admin/audit` SHALL list rows newest first, `Admin:PageSize` (start 100) at a time; `before=<id>` gives older rows.

#### Scenario: Setting change
- **WHEN** admin 1 sets `Relay:MaxTextLength` from `4096` to `10`
- **THEN** the newest audit row holds user 1, `setting.set`, `Relay:MaxTextLength`, the old value `4096` and the new value `10`

#### Scenario: Paging
- **GIVEN** `Admin:PageSize` is 2 and there are 3 audit rows
- **WHEN** an admin lists the audit log, then lists with `before` the id of the last row shown
- **THEN** the first answer has the 2 newest rows and the second the oldest one

### Requirement: ADM-10 Search the access log
`GET /api/admin/access-log` with exactly one of `user=<id>`, `bot=<id>`, `phone=<number>`, `email=<address>` or `address=<ip>` SHALL list the matching rows (actor, actor id, phone, email, address, port, UTC time), newest first, `Admin:PageSize` at a time, `before=<id>` for older ones. Phone in any Iranian form, email in any case, and IPv4 written as IPv6 SHALL match. No filter, more than one, or a bad value: 400 `bad_request`.

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
`GET /api/admin/users?q=<text>` SHALL find users by exact id, phone (any form), email (any case), username or name start; with no `q`, every user. Newest first, `Admin:PageSize` a page, `before=<id>` the next. Each: id, phone, email, name, username, created, `builder`, `suspended`, `bots` (count), `last_active_at`. `?builder=requested`: open requests, oldest first. `POST .../{id}/builder` `{"approve"}` (AUTH-19), `.../{id}/suspend` `{"suspended"}` (AUTH-20). Unknown id: 404 `user_not_found`.

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

### Requirement: ADM-15 Public addresses and llms.txt
`Admin:SiteUrl` (start `https://dalanet.ir`) and `Admin:BotApiUrl` (start `https://api.dalanet.ir`) SHALL be admin settings. `GET /api/public/settings` SHALL also give them as `site_url` and `bot_api_url`. `GET /llms.txt` SHALL need no session and give a plain text page (`text/plain; charset=utf-8`) in English for AI agents that says how to connect a bot, how to get an access key, and how to use the MCP door and the CLI, with the two addresses from the settings at the time of the call.

#### Scenario: Read llms.txt
- **WHEN** a visitor with no session asks for `/llms.txt`
- **THEN** the answer is 200 `text/plain` and names `https://api.dalanet.ir`, `/api/mcp`, `dalan.mjs` and access keys

#### Scenario: Panel change
- **WHEN** an admin sets `Admin:BotApiUrl` to `https://bots.example.ir`
- **THEN** the next `/llms.txt` and `/api/public/settings` use `https://bots.example.ir`

### Requirement: ADM-16 Email code requests are recorded
For every login code that is sent by email the server SHALL keep an access log row (actor `email`) with the email address, the real client address (SEC-10) of the caller who asked for it, the source port when known, and the UTC time. Every sent email gets its own row. A request that sends no email writes no row. ADM-03 applies to these rows too.

#### Scenario: Code sent
- **WHEN** a caller at `203.0.113.7` port `51000` asks a login code for `sara@example.com` and the email is sent
- **THEN** one row holds that address, `203.0.113.7`, `51000` and the time of the call

#### Scenario: Refused request
- **WHEN** a caller asks a code too soon and gets 429 `email_wait`
- **THEN** no new row is written

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

### Requirement: ADM-19 Described settings with ranges
`GET /api/admin/settings` SHALL give, with each setting, its group, name and help in Persian and English, its unit, its `min` and `max` for a number, and `risky`, from a description file kept with the settings catalog. A setting with no description SHALL still be listed, with its key as the name. `PUT` of a number outside its range SHALL get 400 `out_of_range` and change nothing. `GET /api/admin/overview` SHALL give the counts of CLI-82. Both SHALL need an admin (ADM-05).

#### Scenario: Described
- **WHEN** an admin reads the settings
- **THEN** `Admin:AccessLogRetentionDays` has group "Admin", a name and help in both languages, unit "days" and a `min` and `max`

#### Scenario: Out of range
- **GIVEN** `Admin:AccessLogRetentionDays` has `min` 30
- **WHEN** an admin sets it to 5
- **THEN** the answer is 400 `out_of_range` and the value stays

#### Scenario: Every key described
- **WHEN** the tests list the catalog's keys
- **THEN** each has a description, or the test names the missing ones

### Requirement: ADM-20 One user or bot for the admin
`GET /api/admin/users/{id}` SHALL give `{"user", "bots"}`: the user as in ADM-11 and every bot they own as in ADM-12. `GET /api/admin/bots/{id}` SHALL give `{"bot", "owner"}`: the bot as in ADM-12 and its owner as in ADM-11. Unknown ids get 404 `user_not_found` or `bot_not_found`. Each call is in the access log, like every admin call.

#### Scenario: A user with bots
- **GIVEN** user 7 owns bots 5 and 6
- **WHEN** an admin asks `GET /api/admin/users/7`
- **THEN** the answer has user 7 and bots 5 and 6

#### Scenario: A bot and its owner
- **WHEN** an admin asks `GET /api/admin/bots/5`
- **THEN** the answer has bot 5 and user 7 as its owner
