## ADDED Requirements

### Requirement: CLI-74 Command suggestions while typing
When the input starts with `/` and has no space, the client SHALL list above it the bot's commands (REL-26) whose name starts with the typed text, ignoring case, in the bot's order, each with `/command` and its description. A click, a tap or Enter on the marked row SHALL send `/command`; Tab SHALL put `/command ` in the input. The list SHALL close when nothing matches, after a space, when the input is empty, and when a message is sent.

#### Scenario: Slash shows all
- **GIVEN** the bot set the commands "start", "ticket" and "help"
- **WHEN** the user types `/`
- **THEN** the list shows `/start`, `/ticket` and `/help` with their descriptions, in that order

#### Scenario: Narrow by the start
- **WHEN** the user types `/TiC`
- **THEN** the list shows only `/ticket`, marked

#### Scenario: Send by Enter or tap
- **WHEN** the list shows `/ticket` marked and the user presses Enter, or taps it
- **THEN** the message `/ticket` is sent, the input is empty and the list is closed

#### Scenario: Complete with Tab
- **WHEN** the list shows `/ticket` marked and the user presses Tab
- **THEN** the input is `/ticket ` and the list is closed

#### Scenario: Closed
- **WHEN** the user types `/xyz`, or `/ticket 12`, or text not starting with `/`, or the bot has no commands
- **THEN** no list is shown

#### Scenario: Move and close
- **WHEN** the list is shown and the user presses ↓, ↑ or Escape
- **THEN** the mark moves down or up from the first row, or the list closes; the page reads left to right in both languages
