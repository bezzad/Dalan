## MODIFIED Requirements

### Requirement: CLI-26 Landing page
A visitor who is not logged in SHALL see at `/` a landing page in the current theme (CLI-35): a "Problems we solve" menu; a hero with "Enter"; product cards with a traffic picture (bot relay, where the computer only calls out; sites, marked soon); OS tabs with real commands built from `site_url`; three steps using `bot_api_url`; the three messages of DESIGN section 13 in order; a security list; a closing call; a footer. It SHALL NOT say "tunnel". A logged-in user at `/` SHALL get the bot list.

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by mobile number without building SMS login; keeps working when international internet is cut. None says "tunnel"

#### Scenario: Diagram parts
- **WHEN** the bot card picture is read in Persian or English
- **THEN** it goes from the users through Dalan to your computer, and the link to the computer says the call goes out from the computer only

#### Scenario: Steps use the real addresses
- **GIVEN** the public settings say `bot_api_url` is `https://api.example.ir` and `site_url` is `https://example.ir`
- **WHEN** the steps are made in Persian and in English
- **THEN** there are three steps, the second shows `https://api.example.ir/bot<token>/getUpdates`, the third shows `https://example.ir/@`, and no step says "tunnel"

#### Scenario: Which screen
- **WHEN** the address is `/`, `/privacy`, `/faq`, `/showcase` or `/@bot`, for a visitor and for a logged-in user
- **THEN** a visitor gets landing, privacy, FAQ, showcase and login; a logged-in user gets the bot list, privacy, FAQ, showcase and the chat

#### Scenario: Drawn in the page
- **WHEN** the landing page is drawn
- **THEN** the diagram and icons are made in the page, nothing is loaded from another host, and the moving dots stop when the visitor asks for reduced motion

#### Scenario: Try it on every system
- **GIVEN** the public settings say `site_url` is `https://example.ir`
- **WHEN** the commands are made
- **THEN** there are tabs for Linux, macOS, Windows and Docker, and each command gets `https://example.ir/dalan.mjs`

#### Scenario: Only real things are offered
- **WHEN** the menu and the product cards are read
- **THEN** every item that is not built yet is marked "soon", and no text says "tunnel"

## ADDED Requirements

### Requirement: CLI-35 Light and dark theme
The client SHALL have a light and a dark theme. With no choice it SHALL follow the device's theme. The public pages and the bot list SHALL have a theme button that moves system, light, dark and back to system; the choice SHALL be kept on this device and used on the next open. Code blocks SHALL stay dark in both themes.

#### Scenario: First open follows the device
- **WHEN** a visitor opens the site for the first time
- **THEN** the theme is "system": the page has no `data-theme`, and the style sheet gives dark colours when the device asks for dark

#### Scenario: Choice is kept
- **WHEN** the visitor presses the theme button twice
- **THEN** the theme is light, then dark (`data-theme="dark"`), and the next open is dark; a third press goes back to system
