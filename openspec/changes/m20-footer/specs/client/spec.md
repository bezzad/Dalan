## ADDED Requirements

### Requirement: CLI-72 Copyright and version in the footer
Under the footer columns of every public page SHALL be one small grey line: `© <year> Dalan · <version>` in English and `© <year in Persian digits> دالان · <version>` in Persian, where `<version>` and the year come from `/health` (OPS-17). The version SHALL keep Latin digits, read left to right in both languages, and have the commit as its tooltip. When `/health` fails, the line SHALL be `© <current year> Dalan` without a version.

#### Scenario: Live version
- **GIVEN** `/health` gives `version` `v26.11.25.114726` and `commit` `7207d600e166`
- **WHEN** an English public page opens
- **THEN** the footer line is `© 2026 Dalan · v26.11.25.114726`, and the version's tooltip is `7207d600e166`

#### Scenario: Persian
- **WHEN** the same page opens in Persian
- **THEN** the line is `© ۲۰۲۶ دالان · v26.11.25.114726`, with the version left to right

#### Scenario: No answer
- **WHEN** `/health` fails
- **THEN** the line is `© <current year> Dalan` with no version

### Requirement: CLI-73 Footer at the bottom
On a public page shorter than the screen, the footer SHALL sit at the bottom of the screen; on a longer page it SHALL come after the content. The footer SHALL NOT be fixed while scrolling and SHALL NOT cover content, at every screen size (320px to wide, portrait and landscape).

#### Scenario: Short page
- **WHEN** a page with little content (for example `/contact`) opens on a 1920 by 1080 screen
- **THEN** the bottom of the footer is at the bottom of the screen, and the free space is above the footer

#### Scenario: Long page
- **WHEN** the landing page opens on a 360 by 740 phone
- **THEN** the footer comes after the last section, and only shows after scrolling to the end
