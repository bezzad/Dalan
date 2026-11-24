# Client

## ADDED Requirements

### Requirement: CLI-56 About page
`/about` SHALL be a public page that says what Dalan does, in the current language only: Persian when Persian is on, English when English is on. It SHALL NOT name the owner. The footer of every public page SHALL link `/about` and `/contact`.

#### Scenario: One language at a time
- **WHEN** the about texts are read in Persian and in English
- **THEN** each has the same number of parts, the Persian has Persian letters and the English has none

#### Scenario: Footer links
- **WHEN** a public page is drawn
- **THEN** its footer links `/about` and `/contact`

#### Scenario: Which screen
- **WHEN** the address is `/about` or `/contact`, for a visitor and for a logged-in user
- **THEN** both get the about page and the contact page, and the server gives the page at both addresses

### Requirement: CLI-57 Contact page
`/contact` SHALL be a public page with an email field and a text box (at most `contact_max_length` characters, ADM-17) and a send button. For a visitor, or a logged-in user with no email, the email field SHALL be empty and required. For a logged-in user with an email it SHALL hold that email and be read only. After a send that works the text box SHALL be emptied and a short "sent" note shown; a failure SHALL show its error.

#### Scenario: Visitor
- **WHEN** a visitor opens the contact page
- **THEN** the email field is empty, required and not read only

#### Scenario: User with an email
- **GIVEN** the user logged in with `sara@example.com`
- **WHEN** the contact page is drawn
- **THEN** the email field holds `sara@example.com` and is read only
