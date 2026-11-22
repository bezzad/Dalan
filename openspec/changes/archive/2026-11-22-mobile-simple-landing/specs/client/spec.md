## ADDED Requirements

### Requirement: CLI-42 Simple header and a phone menu
The site header SHALL link only to Plans, Showcase and Guide; FAQ and Privacy SHALL be only in the footer. Below 760px the links SHALL fold into a round ☰ button after the header tools, which opens a panel with the same links; a link, a click outside or Escape SHALL close it. A tick icon in a list SHALL sit in the middle of the first text line. No page SHALL scroll sideways on a phone, in portrait or in landscape.

#### Scenario: Header and footer links
- **WHEN** a public page is drawn in Persian or English
- **THEN** the header links go to `/#plans`, `/showcase` and `/guide`, none goes to `/faq` or `/privacy`, and the footer links go to both

#### Scenario: Phone menu
- **WHEN** the header is drawn and the ☰ button is pressed, then pressed again
- **THEN** the panel opens with the three links and `aria-expanded` is `true`, then it closes and `aria-expanded` is `false`

#### Scenario: Folded on a phone
- **WHEN** the style sheet is read
- **THEN** below 760px the header links are hidden and the ☰ button is shown; above it the ☰ button is hidden

#### Scenario: List icons line up
- **WHEN** a list with tick icons (what you get, security, the guards of a plan) is drawn in Persian or English, at any width
- **THEN** each icon sits in the middle of the first line of its text, by the line height of the font, never by a fixed offset

#### Scenario: No sideways scroll
- **WHEN** the landing, guide, showcase, FAQ, privacy, login, bot list and chat pages are open in a browser at 360 by 740 and at 740 by 360 pixels
- **THEN** no page is wider than the screen

## MODIFIED Requirements

### Requirement: CLI-01 The server serves the client
The server SHALL serve the PWA page at `/`, `/bots`, `/apps`, `/privacy`, `/faq`, `/showcase`, `/guide`, `/guide/bot`, `/guide/site` and `/@<bot username>`, and its files (scripts, styles, font, icons) from the same origin. The page SHALL be sent with `Cache-Control: no-cache`, so a new release is seen at the next open. Any other unknown path SHALL get 404.

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response is 200 HTML with `Cache-Control: no-cache`

#### Scenario: Open a bot link
- **WHEN** a browser asks for `/@ArcaneBot`
- **THEN** the response is the same page

#### Scenario: Open a public page
- **WHEN** a browser asks for `/privacy`, `/faq`, `/showcase`, `/guide`, `/guide/bot` or `/guide/site`
- **THEN** the response is the same page

#### Scenario: Unknown path
- **WHEN** a browser asks for `/nothing-here`
- **THEN** the response is 404

### Requirement: CLI-26 Landing page
A visitor and a logged-in user SHALL see at `/` a landing page in the current theme (CLI-35) that says only what Dalan does, its features and its plans, in this order: a hero with "Try it (free)" and "See plans"; the plans (`#plans`), product cards with a traffic picture, each with "Get started" to its guide (CLI-38); what you get (CLI-37); the three messages of DESIGN section 13; a security list; a closing call; a footer. It SHALL NOT show install steps or code, or say "tunnel".

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by email or SMS without building a login; keeps working when outside links are down (Persian: when international internet is cut). None says "tunnel"

#### Scenario: Diagram parts
- **WHEN** the bot card picture is read in Persian or English
- **THEN** it goes from the users through Dalan to your computer, and the link to the computer says the call goes out from the computer only

#### Scenario: Each plan opens its guide
- **WHEN** the plans are read in Persian or English
- **THEN** there are two, bot relay and sites, both built, and their guides are `/guide/bot` and `/guide/site`

#### Scenario: No install steps
- **WHEN** the landing page is drawn
- **THEN** it has no code block, no command tabs and no steps

#### Scenario: Which screen
- **WHEN** the address is `/`, `/bots`, `/privacy`, `/faq`, `/showcase` or `/@bot`, for a visitor and for a logged-in user
- **THEN** a visitor gets landing, login, privacy, FAQ, showcase and login; a logged-in user gets landing, the bot list, privacy, FAQ, showcase and the chat

### Requirement: CLI-37 What you get
The landing page SHALL have a "What you get" section after the plans: four groups in order (bot relay, users and safety, tools for builders, sites and gateway), each with a title, a short text and a list. The first three SHALL list only built things. In the fourth, the first item (a local site at a fixed Dalan subdomain, CLI-39) is built; every other item SHALL be marked "soon". One line SHALL say what Dalan does not offer. No text SHALL say "tunnel".

#### Scenario: Ready and soon
- **WHEN** the groups are read in Persian or English
- **THEN** there are four, no thing in the first three is marked soon, the first thing of the fourth is not marked soon, and every other thing in the fourth is marked soon

#### Scenario: Honest about what is not offered
- **WHEN** the not-offered line is read in Persian or English
- **THEN** it names TCP, SSH and the AI gateway, and no text of the section says "tunnel"

### Requirement: CLI-38 Guide to run a bot
A visitor and a logged-in user SHALL see at `/guide` a choice of plan: a bot or a site. `/guide/bot` SHALL show, in both languages, nine steps to run a bot without an AI agent. `/guide/site` SHALL show four steps: become a builder, install the agent in one line (CLI-39), log in from "My apps", run `dalan http`. Addresses and numbers SHALL come from the public settings. "My bots" SHALL show steps 5 to 9 of the bot guide itself.

#### Scenario: Steps use the real addresses
- **GIVEN** the public settings say `bot_api_url` is `https://api.example.ir`, `site_url` is `https://example.ir` and the expiry is 60 seconds
- **WHEN** the guide is made in Persian and in English
- **THEN** both have nine steps; the token test is `curl https://api.example.ir/bot<token>/getMe`; the run step shows `https://example.ir/@`; the last step says 60 seconds

#### Scenario: Every library points at Dalan
- **WHEN** the library code is made for `https://api.example.ir`
- **THEN** there are seven samples (python-telegram-bot, aiogram, pyTelegramBotAPI, Telegraf, grammY, node-telegram-bot-api, Telegram.Bot), each has `https://api.example.ir` and none has `api.telegram.org`

#### Scenario: Site guide
- **GIVEN** the public settings say `site_url` is `https://example.ir`
- **WHEN** the site guide is made in Persian and in English
- **THEN** both have four steps; the install step has `curl -fsSL https://example.ir/install.sh | sh` and `irm https://example.ir/install.ps1 | iex`; the last step has `dalan http 3000 --name mysite`

#### Scenario: Which screen
- **WHEN** the address is `/guide`, `/guide/bot` or `/guide/site`, for a visitor or a logged-in user
- **THEN** both get the plan choice, the bot guide and the site guide
