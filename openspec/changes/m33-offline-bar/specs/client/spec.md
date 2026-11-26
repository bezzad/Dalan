## ADDED Requirements

### Requirement: CLI-91 Offline bar in the chat
While the bot of the open chat is offline (its status, CLI-19, has `online` false, with or without `last_seen`), the chat SHALL show a bar above the input that says the bot is offline and will not answer now, and that messages wait for it and show "not delivered" if it does not come back in time. The bar SHALL name the bot as `@username`. It SHALL hide when the status says online, and SHALL NOT show for a suspended bot (CLI-25). A failed status call SHALL leave the bar as it was. Sending is not blocked.

#### Scenario: Bot offline
- **GIVEN** the chat of `@mofid` is open and its status is `online` false
- **WHEN** the status is drawn in English
- **THEN** a bar above the input says "@mofid is offline and won't answer now" and mentions "not delivered"

#### Scenario: Bot comes back
- **GIVEN** the offline bar is shown
- **WHEN** the next status is `online` true
- **THEN** the bar is hidden

#### Scenario: Suspended
- **WHEN** a suspended bot is offline
- **THEN** the bar is hidden; the suspended error shows instead
