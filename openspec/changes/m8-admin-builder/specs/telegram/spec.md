## ADDED Requirements

### Requirement: TG-22 Suspended bot
A Telegram adapter call of a suspended bot (REL-38) SHALL get HTTP 403 with `{"ok":false,"error_code":403,"description":"Forbidden: bot was suspended"}` and SHALL do nothing.

#### Scenario: getMe
- **GIVEN** a suspended bot
- **WHEN** it calls `getMe`
- **THEN** the answer is 403 with that description
