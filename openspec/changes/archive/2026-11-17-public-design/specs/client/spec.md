## MODIFIED Requirements

### Requirement: CLI-26 Landing page
A visitor who is not logged in SHALL see at `/` the landing page: a hero with an "Enter" button; a diagram of the user, Dalan and the builder's computer, where the computer only calls out; three steps whose code shows `bot_api_url` and `site_url` from the public settings; the three messages of DESIGN section 13 in order; links to the showcase, FAQ and privacy page. It SHALL NOT say "tunnel". A logged-in user at `/` SHALL get the bot list. The public pages SHALL open with or without login.

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by mobile number without building SMS login; keeps working when international internet is cut. None says "tunnel"

#### Scenario: Diagram parts
- **WHEN** the diagram labels are read in Persian or English
- **THEN** there are three parts in order (user, Dalan, your computer), and the link to the computer says the call goes out from the computer only

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
