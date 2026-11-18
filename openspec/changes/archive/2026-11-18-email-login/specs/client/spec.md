# Spec Delta

## MODIFIED Requirements

### Requirement: CLI-23 Builder request in the account screen
The account screen SHALL show the user's builder state: a button "become a builder" when it is `none`, "waiting for approval" when `requested`, and a button that opens "My bots" when `approved` or for an admin. A user with no phone SHALL see, instead of the request button, a note that building needs a verified mobile number (CLI-34). Admins SHALL also see a button that opens the admin screen.

#### Scenario: What the account screen shows
- **WHEN** the builder state is `none`, `requested` or `approved`, or the user is an admin
- **THEN** the screen shows the request button, the waiting note, the "My bots" button, or the "My bots" and "Admin" buttons

#### Scenario: No phone
- **WHEN** a user with no phone and builder state `none` opens the account screen
- **THEN** the screen shows the "verify your mobile first" note and no request button

## ADDED Requirements

### Requirement: CLI-33 Email login screen
When `email_login` is on (AUTH-16), the login screen SHALL offer "log in with email": the email address, the proof of work (CLI-10), then the code and, for a new address, the name. After the code is sent the screen SHALL say that the code was sent and that the person should check the spam folder too. Errors `bad_email`, `email_wait`, `email_limit` and `email_unavailable` SHALL have their own texts in both languages.

#### Scenario: Spam note
- **WHEN** the email code was sent
- **THEN** the screen shows the address and the note to check the spam folder

#### Scenario: Texts
- **WHEN** the client gets `bad_email`, `email_wait`, `email_limit`, `email_unavailable`, `phone_required`, `phone_taken` or `has_phone`
- **THEN** it shows its own text, in Persian and in English

### Requirement: CLI-34 Verify a mobile number in the account screen
A user with no phone (AUTH-04 `has_phone` false) SHALL see a card in the account screen to add a mobile number: the number, the proof of work, then the SMS code (AUTH-26). After success the card SHALL go away and the builder request button SHALL show.

#### Scenario: Level 1 user
- **WHEN** a user with `has_phone` false opens the account screen
- **THEN** the "verify your mobile" card is shown; with `has_phone` true it is not
