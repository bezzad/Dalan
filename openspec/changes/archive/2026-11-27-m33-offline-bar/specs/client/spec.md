## ADDED Requirements

### Requirement: CLI-91 Offline bar in the chat
While the bot of the open chat is offline (its status, CLI-19, has `online` false), the chat SHALL show a bar above the input. The bar SHALL name the bot as `@username`, say it will not answer now, and say messages wait and show "not delivered" if it does not come back in time. It SHALL NOT show for an online or suspended bot (CLI-25). Sending is not blocked.

#### Scenario: Bot offline
- **GIVEN** the chat of `@mofid` is open and its status is `online` false, with or without `last_seen`
- **WHEN** the status is drawn in English
- **THEN** a bar above the input says "@mofid is offline and won't answer now" and mentions "not delivered"

#### Scenario: Bot comes back
- **GIVEN** the offline bar is shown
- **WHEN** the next status is `online` true
- **THEN** the bar is hidden

#### Scenario: Status call fails
- **WHEN** a status call fails
- **THEN** the bar stays as it was (hidden before the first status)

#### Scenario: Suspended
- **WHEN** a suspended bot is offline
- **THEN** the bar is hidden; the suspended error shows instead
