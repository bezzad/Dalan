## ADDED Requirements

### Requirement: CLI-72 Copyright and version in the footer
Under the footer columns of every public page SHALL be one small grey line: `© <this year> Dalan <version>` in English and `© <this year in Persian digits> دالان <version>` in Persian. `<version>` (OPS-17) SHALL come from `GET /api/public/settings` (`version`), because `/health` is private (OPS-06); it SHALL keep Latin digits and read left to right in both languages. Without a version like `v1.1.0` (no answer, or a local run) the line SHALL have none.

#### Scenario: Live version
- **GIVEN** the public settings give `version` `v1.1.0` in 2026
- **WHEN** an English public page opens
- **THEN** the footer line is `© 2026 Dalan v1.1.0`

#### Scenario: Persian
- **WHEN** the same page opens in Persian
- **THEN** the line is `© ۲۰۲۶ دالان v1.1.0`, with the version left to right

#### Scenario: No version
- **WHEN** the public settings fail, or give `dev`
- **THEN** the line is `© <this year> Dalan` with no version

### Requirement: CLI-73 Footer at the bottom
On a public page shorter than the screen, the footer SHALL sit at the bottom of the screen; on a longer page it SHALL come after the content. The footer SHALL NOT be fixed while scrolling and SHALL NOT cover content, at every screen size (320px to wide, portrait and landscape).

#### Scenario: Short page
- **WHEN** a page with little content (for example `/contact`) opens on a 1920 by 1080 screen
- **THEN** the bottom of the footer is at the bottom of the screen, and the free space is above the footer

#### Scenario: Long page
- **WHEN** the landing page opens on a 360 by 740 phone
- **THEN** the footer comes after the last section, and only shows after scrolling to the end
