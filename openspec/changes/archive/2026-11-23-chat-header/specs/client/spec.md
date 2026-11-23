## ADDED Requirements

### Requirement: CLI-45 The chat page is the bot's own page
The chat page SHALL have one header, in this order: the bot's icon (its showcase icon, else the first letter of its name), its name with a line of `@username` and its status (CLI-19), a search button, the theme button, the user menu shown as a round initial, and the Dalan mark that goes to `/`. It SHALL NOT have a Back button, the language button or the Dalan name. Search SHALL show only the loaded messages whose text or file name has the words, without regard to case; Escape or ✕ SHALL close it and show every message again.

#### Scenario: Header parts
- **WHEN** the chat header of `@mofid`, named `Arcane`, is drawn
- **THEN** it shows the letter `A`, `Arcane`, `@mofid`, then search, theme, the user menu and a link to `/`, and no Back or language button

#### Scenario: Search
- **GIVEN** messages "Hello", "the clock" and a file `Clock.png`
- **WHEN** the search has `clock`
- **THEN** only "the clock" and `Clock.png` are shown; with an empty search all three are shown
