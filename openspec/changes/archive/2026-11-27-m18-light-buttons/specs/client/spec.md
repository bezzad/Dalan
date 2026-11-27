## ADDED Requirements

### Requirement: CLI-71 Plain buttons are visible in the light theme
In the light theme the border of a plain button (`button`, `a.button`, the login way buttons) SHALL use the colour `--control-line`, with a contrast of at least 3:1 against `--surface` and against `--bg` (WCAG 2.2, 1.4.11). Cards, rows and dividers SHALL keep `--line`. Primary, link and coloured buttons, and the dark theme, SHALL NOT change.

#### Scenario: Border contrast
- **WHEN** the light `:root` colours of `app.css` are read
- **THEN** the contrast of `--control-line` against `--surface` and against `--bg` is each at least 3:1

#### Scenario: Buttons use the strong border
- **WHEN** the login screen opens in the light theme
- **THEN** the SMS, GitHub and email buttons and every plain button have a border of `--control-line`, and the card keeps its `--line` border

#### Scenario: Dark theme unchanged
- **WHEN** the theme is dark
- **THEN** plain buttons look as before (`--control-line` is the dark `--line`)
