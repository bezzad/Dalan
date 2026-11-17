## MODIFIED Requirements

### Requirement: CLI-01 The server serves the client
The server SHALL serve the PWA page at `/`, `/privacy`, `/faq`, `/showcase` and `/@<bot username>`, and its files (scripts, styles, font, icons) from the same origin. The page SHALL be sent with `Cache-Control: no-cache`, so a new release is seen at the next open. Any other unknown path SHALL get 404.

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

## ADDED Requirements

### Requirement: CLI-26 Landing page
A visitor who is not logged in SHALL see at `/` the landing page: the three messages of DESIGN section 13 in their order, an "Enter" button that opens the login screen, and links to the showcase, the FAQ and the privacy page. It SHALL NOT say "free tunnel". A logged-in user at `/` SHALL still get the bot list. The privacy, FAQ and showcase pages SHALL open with or without login.

#### Scenario: Three messages in order
- **WHEN** the landing texts are read in Persian or English
- **THEN** they are, in this order: run your app from your own computer; real users verified by mobile number without building SMS login; keeps working when international internet is cut. None says "tunnel"

#### Scenario: Which screen
- **WHEN** the address is `/`, `/privacy`, `/faq`, `/showcase` or `/@bot`, for a visitor and for a logged-in user
- **THEN** a visitor gets landing, privacy, FAQ, showcase and login; a logged-in user gets the bot list, privacy, FAQ, showcase and the chat

### Requirement: CLI-27 Privacy page and FAQ
The privacy page SHALL say plainly, in both languages, the six points of DESIGN section 13 (showcase only on request, no phone number to bots, no end-to-end encryption, history kept, the home machine is the builder's, the access log). The numbers SHALL come from ADM-14, never from the text. The FAQ SHALL answer the same six points as questions.

#### Scenario: Numbers from the server
- **GIVEN** the public settings say 200 messages, 30 days and 180 days of access log
- **WHEN** the privacy page and the FAQ are drawn in Persian and in English
- **THEN** each has six points, and the numbers 200, 30 and 180 are in them (in the language's digits)

### Requirement: CLI-28 Showcase page
The showcase page SHALL show the listed bots (icon or first letter, name, `@username`, description, category), a filter by category and a "more" button while a full page came back. "Open" SHALL go to `/@username`; a visitor logs in first and then lands in that chat.

#### Scenario: Category names
- **WHEN** the categories `games` and `robots` are drawn
- **THEN** `games` has its Persian or English name and `robots` is shown as it is

### Requirement: CLI-29 Showcase form and review
In "My bots" each bot SHALL have a showcase part: its state (not listed, waiting for review, listed, refused), a form for description and category, an icon picker, and a "leave the showcase" button. It SHALL say that every change waits for review. The admin screen SHALL have a "showcase" tab with the pending entries (icon, name, username, description, category) and approve and refuse buttons.

#### Scenario: State names
- **WHEN** a bot with no entry, a pending, a listed and a refused entry are drawn
- **THEN** they say "not listed", "waiting for review", "listed" and "refused"
