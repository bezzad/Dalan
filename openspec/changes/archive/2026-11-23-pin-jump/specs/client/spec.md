## MODIFIED Requirements

### Requirement: CLI-14 Pinned message bar
The client SHALL show the pinned message of the chat (REL-25) in a bar above the messages, taken from the `pinned` field of the newest messages answer and kept up to date by changes: a message with `pinned` true becomes the bar; the bar's message without `pinned`, or deleted, clears it. A click on the bar SHALL close the search, load older pages until the message is there, scroll to it and mark it for a moment; a message that is not in the history gives a short note.

#### Scenario: Pin moves
- **GIVEN** the bar shows message 2
- **WHEN** message 2 arrives without `pinned` and then message 3 with `pinned` true
- **THEN** the bar shows message 3

#### Scenario: Unpin
- **GIVEN** the bar shows message 2
- **WHEN** message 2 arrives without `pinned`
- **THEN** there is no bar

#### Scenario: Jump to an older pinned message
- **GIVEN** the pinned message 2 is older than the loaded page
- **WHEN** the bar is clicked
- **THEN** older pages load until message 2 is there, and the chat scrolls to it

#### Scenario: Pinned message not in the history
- **GIVEN** the pinned message is older than every message the server keeps
- **WHEN** the bar is clicked
- **THEN** older pages load to the start, and a note says it is not in the history
