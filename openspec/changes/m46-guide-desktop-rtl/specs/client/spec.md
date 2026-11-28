## ADDED Requirements

### Requirement: CLI-101 Persian is a mirror of English
In Persian every public page SHALL be the mirror of the English one: a column narrower than the page SHALL start at the right edge of the page, never in the middle.

#### Scenario: Persian is a mirror
- **GIVEN** the page language is Persian
- **WHEN** `/faq` or `/guide/site` opens on a computer
- **THEN** the title and the cards start at the right edge of the page, where the English page has them at the left edge

### Requirement: CLI-102 The guides on a computer
On a screen 960px or wider, a guide SHALL use the width of the page: the step names SHALL be a list at the start side, and the open step SHALL be one big card beside it, with its title on top. One step SHALL always be open there. On a narrower screen the steps SHALL stay cards one under another (CLI-64), as wide as the page.

#### Scenario: The guide on a computer
- **GIVEN** a window 1440px wide
- **WHEN** the site guide opens
- **THEN** the step names are at the start side, the open step is a card more than 600px wide beside them, and together they fill the page width

#### Scenario: The guide on a phone
- **GIVEN** a phone
- **WHEN** the site guide opens
- **THEN** the steps are cards one under another, as wide as the page, with no sideways scroll
