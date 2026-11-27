## MODIFIED Requirements

### Requirement: CLI-52 Reply in the chat
The user SHALL start a reply by a message's menu (right-click, or on a touch screen a long press of `Client:LongPressMs`, start 500, with no move) → Reply or Copy, a double-click, or a swipe to the left; a long press SHALL NOT open the system's text menu. A bar above the input SHALL show the quote, with ✕ and Esc to cancel; the next message or file SHALL carry `reply_to` (REL-52). A replying message SHALL show the quote on top; a click on it SHALL go to that message like CLI-14.

#### Scenario: Reply to an answer
- **GIVEN** a bot answer in the chat
- **WHEN** the user opens its menu and picks Reply, then sends "ok"
- **THEN** the message "ok" has `reply_to` of that answer and shows its quote

#### Scenario: Long press on a phone
- **GIVEN** a touch screen
- **WHEN** the user holds a finger on a bot message for the long-press time without moving
- **THEN** the menu with Reply and Copy opens there, and Copy puts the message text on the clipboard

#### Scenario: A scroll is not a long press
- **WHEN** the finger moves more than 10 pixels before the time
- **THEN** no menu opens

#### Scenario: Cancel
- **GIVEN** the reply bar is open
- **WHEN** the user presses ✕
- **THEN** the bar is gone and the next message has no `reply_to`

#### Scenario: Quote line
- **GIVEN** a message replies to a bot message "line one\nline two"
- **WHEN** the chat is shown
- **THEN** the quote shows the bot's name and "line one"

#### Scenario: Target deleted
- **GIVEN** a message replies to message 2, and message 2 was deleted
- **WHEN** the chat is shown
- **THEN** the quote says "Deleted message"


### Requirement: CLI-78 Grouped user menu
The user menu SHALL open with the user's name and, in grey, the phone or the email. Under it SHALL come groups with an icon per item and a line between groups: My bots, Chats, My apps; then Dashboard; then Log out alone. Dashboard SHALL open `/dashboard` for everyone (owner, 2026-11-27); Settings and Admin are in the dashboard's side menu.

#### Scenario: Admin
- **GIVEN** an admin with the email `a@example.com` and no phone
- **WHEN** the user menu opens
- **THEN** it shows the name, `a@example.com` in grey, then three groups: (My bots, Chats, My apps), (Dashboard), (Log out), each item with an icon, and Dashboard opens `/dashboard`

#### Scenario: Not an admin
- **WHEN** a user who is not an admin opens the menu
- **THEN** the second group has only Dashboard, which opens `/dashboard`

### Requirement: CLI-23 Builder request in the account screen
A user with a verified phone SHALL be a builder with no request (AUTH-39): no "become a builder" button and no "waiting for approval". A user with no phone SHALL see, on the Builder section and on My bots, a note that building needs a verified mobile number, with a button to verify it (CLI-34). A user the admin refused SHALL see that note. For a builder the dashboard SHALL show My bots and My apps.

#### Scenario: What the account screen shows
- **GIVEN** a user with no phone
- **WHEN** the Builder section opens
- **THEN** it says building needs a verified mobile number and has a button to verify it, and no request button

#### Scenario: A verified phone
- **WHEN** a user with a verified phone opens the Builder section
- **THEN** it says they can build, with a link to My bots, and no request button

#### Scenario: No phone
- **WHEN** a user with no phone and builder state `none` opens the account screen
- **THEN** the screen shows the "verify your mobile first" note and no request button

#### Scenario: Not a builder opens My bots
- **GIVEN** a user who logged in by email, with no phone and builder state `none`
- **WHEN** the user opens "My bots"
- **THEN** the address is `/bots`, the title is "My bots", and the page says to verify the mobile first, with its button


## ADDED Requirements

### Requirement: CLI-94 Colored titles on public pages
The accent line of each big title on public pages (the home page hero and section titles) SHALL be drawn in a gradient of five colors (`--rainbow`, its own set for the dark theme), as text. The gradient SHALL move slowly along the text, like ngrok's titles; with `prefers-reduced-motion` it SHALL stand still. In Persian it SHALL run from the right.

#### Scenario: Home page title
- **WHEN** the home page opens
- **THEN** the second line of the hero title is the colored gradient, moving slowly

#### Scenario: Reduced motion
- **GIVEN** the system asks for reduced motion
- **WHEN** the home page opens
- **THEN** the title is colored and does not move

### Requirement: CLI-95 A new email login says it makes an account
When the email login's code is right but the address belongs to no account (the server asks for a name), the login screen SHALL say, above the name box, that this makes a new account, and that someone who already has an account with a mobile number SHALL log in with it and add the email in Profile instead. The same note SHALL show for a GitHub login that would make a new account.

#### Scenario: New address
- **GIVEN** no account has `sara@example.com`
- **WHEN** Sara enters the right email code
- **THEN** the name box shows with the note that a new account will be made and how to add the email to an existing one
