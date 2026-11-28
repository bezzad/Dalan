## MODIFIED Requirements

### Requirement: CLI-86 The message box grows
The message box SHALL grow with its text up to `--compose-max-height` (about six lines, at most 30% of the screen height, like Telegram) and then scroll. Its buttons SHALL keep their size at the bottom. After a send it SHALL be one row high again.

#### Scenario: Long text
- **WHEN** the user types 30 lines
- **THEN** the box is about six lines high and scrolls, and Send stays its own size at the bottom

#### Scenario: After send
- **WHEN** the user sends 5 lines
- **THEN** the box is one row high and empty

#### Scenario: No scrollbar before the largest size
- **WHEN** the box is empty, or holds one to five short lines
- **THEN** the box shows no scrollbar

#### Scenario: Only the message list scrolls
- **WHEN** a chat is open on a computer or a phone
- **THEN** the page itself is never taller than the window; only the message list (and the box past its largest size) scrolls
