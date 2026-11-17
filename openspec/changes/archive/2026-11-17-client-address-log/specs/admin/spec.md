## ADDED Requirements

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
