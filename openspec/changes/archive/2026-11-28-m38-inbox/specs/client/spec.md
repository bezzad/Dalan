## ADDED Requirements

### Requirement: CLI-96 The bell
Every signed-in page SHALL have a bell in the header with the unread count (none when 0). It SHALL open a panel with the newest notices (unread ones bold) and "More"; opening it SHALL mark them read; a notice with a link SHALL open it. A push for a notice SHALL open its link (CLI-22). The count SHALL refresh with the live stream, or on each screen change.

#### Scenario: Unread
- **GIVEN** two unread notices
- **WHEN** the user opens any signed-in page
- **THEN** the bell shows 2; after the panel opens it shows none

#### Scenario: Open a notice
- **WHEN** the admin taps the notice of a new site
- **THEN** that user's admin page opens
