## MODIFIED Requirements

### Requirement: CLI-01 The server serves the client
The server SHALL serve the PWA page at `/`, `/bots`, `/privacy`, `/faq`, `/showcase` and `/@<bot username>`, and its files (scripts, styles, font, icons) from the same origin. The page SHALL be sent with `Cache-Control: no-cache`, so a new release is seen at the next open. Any other unknown path SHALL get 404.

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response is 200 HTML with `Cache-Control: no-cache`

#### Scenario: Open a bot link
- **WHEN** a browser asks for `/@ArcaneBot`
- **THEN** the response is the same page

#### Scenario: Open a public page
- **WHEN** a browser asks for `/privacy`, `/faq` or `/showcase`
- **THEN** the response is the same page

#### Scenario: Unknown path
- **WHEN** a browser asks for `/nothing-here`
- **THEN** the response is 404

### Requirement: CLI-24 My bots
The "My bots" screen SHALL list the builder's bots (username, name, last seen, suspended) and have a form to create one. A builder or an admin SHALL open it with one press from the bot list ("Create bot"). After a create or a token rotation the screen SHALL show the token once, with a copy button and a note that it is shown only now. Rotation SHALL ask for confirmation first, because the old token stops at once.

#### Scenario: Token shown once
- **WHEN** a builder creates a bot
- **THEN** the token is shown with the copy button, and after closing it the list shows the bot without the token

### Requirement: CLI-26 Landing page
A visitor and a logged-in user SHALL see at `/` a landing page in the current theme (CLI-35): a "Problems we solve" menu; a hero with "Enter"; product cards with a traffic picture (bot relay, where the computer only calls out; sites, marked soon); OS tabs with real commands built from `site_url`; three steps using `bot_api_url`; the three messages of DESIGN section 13 in order; a security list; a closing call; a footer. It SHALL NOT say "tunnel".

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by email or SMS without building a login; keeps working when international internet is cut. None says "tunnel"

#### Scenario: Diagram parts
- **WHEN** the bot card picture is read in Persian or English
- **THEN** it goes from the users through Dalan to your computer, and the link to the computer says the call goes out from the computer only

#### Scenario: Steps use the real addresses
- **GIVEN** the public settings say `bot_api_url` is `https://api.example.ir` and `site_url` is `https://example.ir`
- **WHEN** the steps are made in Persian and in English
- **THEN** there are three steps, the second shows `https://api.example.ir/bot<token>/getUpdates`, the third shows `https://example.ir/@`, and no step says "tunnel"

#### Scenario: Which screen
- **WHEN** the address is `/`, `/bots`, `/privacy`, `/faq`, `/showcase` or `/@bot`, for a visitor and for a logged-in user
- **THEN** a visitor gets landing, login, privacy, FAQ, showcase and login; a logged-in user gets landing, the bot list, privacy, FAQ, showcase and the chat

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
