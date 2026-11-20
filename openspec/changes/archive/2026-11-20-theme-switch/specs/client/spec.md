## MODIFIED Requirements

### Requirement: CLI-35 Light and dark theme
The client SHALL have a light and a dark theme. On the first open it SHALL follow the device's theme. The public pages and the bot list SHALL have a theme button that switches only between light and dark; the choice SHALL be kept on this device and used on the next open, whatever the device says. The switch SHALL show the new theme as a circle that grows from the button, and SHALL change at once when the visitor asks for reduced motion. Code blocks SHALL stay dark in both themes.

#### Scenario: First open follows the device
- **WHEN** a visitor opens the site for the first time on a device that asks for dark
- **THEN** the theme is dark (`data-theme="dark"`), and nothing is kept yet

#### Scenario: Choice is kept
- **WHEN** the visitor presses the theme button twice, starting from light
- **THEN** the theme is dark, then light; there is no third state, and the next open uses the kept choice
