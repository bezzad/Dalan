## MODIFIED Requirements

### Requirement: CLI-26 Landing page
A visitor and a logged-in user SHALL see at `/` a landing page in the current theme (CLI-35) that says only what Dalan does, its features and its plans, in this order: a hero with one call, "Try it (free)"; a strip of what Dalan works with (languages and frameworks, as words); the plans (`#plans`), product cards with a traffic picture, each with "Get started" to its guide (CLI-38); "Try it now" (`#try`): the one install line of the site agent for Linux and macOS or Windows, with Copy, and links to both guides; what you get (CLI-37); the three messages of DESIGN section 13; a security list; a closing call; a footer. It SHALL NOT show other install steps or code, or say "tunnel".

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
- **THEN** it has no code block and no steps; the only command is the install line of "Try it now"

#### Scenario: Try it now
- **GIVEN** the public settings say the site is `https://dalanet.ir`
- **WHEN** the landing page is drawn in Persian or English
- **THEN** "Try it now" shows `curl -fsSL https://dalanet.ir/install.sh | sh`, Windows shows `irm https://dalanet.ir/install.ps1 | iex`, and it links `/guide/site` and `/guide/bot`

#### Scenario: One call in the hero
- **WHEN** the landing page is drawn in Persian or English
- **THEN** the hero has exactly one button, "Try it (free)", and the strip of names follows the hero

#### Scenario: Which screen
- **WHEN** the address is `/`, `/bots`, `/privacy`, `/faq`, `/showcase` or `/@bot`, for a visitor and for a logged-in user
- **THEN** a visitor gets landing, login, privacy, FAQ, showcase and login; a logged-in user gets landing, the bot list, privacy, FAQ, showcase and the chat

## ADDED Requirements

### Requirement: CLI-103 Display font and the hero cloud
The big titles of the home page (the hero, the section titles and the closing call) SHALL use a display font served by Dalan: a serif for Latin letters and a Naskh for Persian. A soft, still cloud of the title colours (`--rainbow`) SHALL sit behind the hero title, fainter in the light theme. The home page SHALL fit a phone with no sideways scroll.

#### Scenario: Display font from Dalan
- **WHEN** the home page opens in English and in Persian
- **THEN** the hero title is drawn in the display font, and every font file comes from Dalan's own origin

#### Scenario: Phone
- **GIVEN** a phone 360px wide
- **WHEN** the home page opens in Persian or English
- **THEN** there is no sideways scroll

### Requirement: CLI-104 A colour per card
Each plan and each "What you get" card SHALL have its own colour (`--tone`): its icon badge, its small label and, on a plan, its traffic picture. On hover a plan card SHALL take a soft tint of its colour and its button SHALL turn light.

#### Scenario: Colours per card
- **WHEN** the home page opens
- **THEN** the two plan badges have two different colours, and the four "What you get" badges four different colours

#### Scenario: Hover
- **WHEN** the pointer is over a plan card
- **THEN** the card is tinted with its colour and its "Get started" button is light

### Requirement: CLI-105 The strip of names
The strip of names under the hero SHALL move slowly toward the end side with faded edges. With `prefers-reduced-motion` it SHALL stand still and wrap.

#### Scenario: Reduced motion
- **GIVEN** the system asks for reduced motion
- **WHEN** the home page opens
- **THEN** the strip of names does not move
