## ADDED Requirements

### Requirement: CLI-41 English text never names Iran
No English text that the product shows SHALL name Iran: client texts, page titles, meta tags, the manifest, `/llms.txt`, and the error texts of the API. Persian text MAY name ایران. Where the English text spoke of "international internet is cut", it SHALL say it neutrally, for example "Keeps working when outside links are down" or "inside the country".

#### Scenario: Client English texts
- **WHEN** every English text of the client (texts, landing, menus, guides, security list) is read
- **THEN** none contains "Iran" in any case

#### Scenario: Built files
- **WHEN** the built client files (HTML, scripts, manifest) are scanned
- **THEN** none contains "Iran" in any case; Persian "ایران" is allowed

#### Scenario: Server texts
- **WHEN** a visitor reads `/llms.txt`, or sends a bad phone number to `/api/auth/sms`
- **THEN** the text does not contain "Iran" in any case

## MODIFIED Requirements

### Requirement: CLI-26 Landing page
A visitor and a logged-in user SHALL see at `/` a landing page in the current theme (CLI-35): a "Problems we solve" menu; a hero with "Enter"; product cards with a traffic picture (bot relay, where the computer only calls out; sites, marked soon); OS tabs with real commands built from `site_url`; three steps using `bot_api_url`; the three messages of DESIGN section 13 in order; a security list; a closing call; a footer. It SHALL NOT say "tunnel".

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by email or SMS without building a login; keeps working when outside links are down (Persian: when international internet is cut). None says "tunnel"

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

### Requirement: CLI-35 Light and dark theme
The client SHALL have a light and a dark theme. On the first open it SHALL follow the device's theme. Every page header SHALL have a round theme button that switches only between light and dark, beside a round language button of the same size that shows only a globe and switches Persian and English in one click (Persian is the default, CLI-04; the page itself shows the change); both SHALL be in the same place in the fixed header of every page. The theme choice SHALL be kept on this device and used on the next open, whatever the device says. The new theme SHALL grow as a circle from the button (at once with reduced motion). Code blocks SHALL stay dark in both themes.

#### Scenario: First open follows the device
- **WHEN** a visitor opens the site for the first time on a device that asks for dark
- **THEN** the theme is dark (`data-theme="dark"`), and nothing is kept yet

#### Scenario: Choice is kept
- **WHEN** the visitor presses the theme button twice, starting from light
- **THEN** the theme is dark, then light; there is no third state, and the next open uses the kept choice

#### Scenario: Language button
- **WHEN** the header tools are made in Persian, then in English
- **THEN** the language button is round and shows only the globe (no letters); its name for screen readers says the other language; one click switches the language

#### Scenario: Same size as the theme button
- **WHEN** a page is open in a browser at desktop and at phone width
- **THEN** the language and theme buttons are both 36 by 36 pixels, side by side in the header
