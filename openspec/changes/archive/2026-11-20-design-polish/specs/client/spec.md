## ADDED Requirements

### Requirement: CLI-40 Closed gate for an unknown address
An address the site does not know SHALL show a "not found" page instead of the landing page: the site header, the Dalan gate drawn closed, one short sentence and a button to the home page.

#### Scenario: Unknown address
- **WHEN** a visitor or a logged-in user opens `/no-such-page`
- **THEN** the page is "not found", with the closed gate and a button to `/`

## MODIFIED Requirements

### Requirement: CLI-35 Light and dark theme
The client SHALL have a light and a dark theme. On the first open it SHALL follow the device's theme. Every page header SHALL have a round theme button that switches only between light and dark, beside a round language button (globe and EN or FA); the choice SHALL be kept on this device and used on the next open, whatever the device says. The new theme SHALL grow as a circle from the button (at once with reduced motion). Code blocks SHALL stay dark in both themes.

#### Scenario: First open follows the device
- **WHEN** a visitor opens the site for the first time on a device that asks for dark
- **THEN** the theme is dark (`data-theme="dark"`), and nothing is kept yet

#### Scenario: Choice is kept
- **WHEN** the visitor presses the theme button twice, starting from light
- **THEN** the theme is dark, then light; there is no third state, and the next open uses the kept choice
