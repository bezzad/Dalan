# Spec Delta

## MODIFIED Requirements

### Requirement: CLI-33 Email login screen
When `email_login` is on (AUTH-16), the first login card (CLI-36) SHALL ask for the email address; then the proof of work (CLI-10), then the code and, for a new address, the name. After the code is sent the screen SHALL say that the code was sent and that the person should check the spam folder too. Errors `bad_email`, `email_wait`, `email_limit` and `email_unavailable` SHALL have their own texts in both languages.

#### Scenario: Spam note
- **WHEN** the email code was sent
- **THEN** the screen shows the address and the note to check the spam folder

#### Scenario: Texts
- **WHEN** the client gets `bad_email`, `email_wait`, `email_limit`, `email_unavailable`, `phone_required`, `phone_taken` or `has_phone`
- **THEN** it shows its own text, in Persian and in English

## ADDED Requirements

### Requirement: CLI-36 One simple login card
The login screen SHALL be one card, like ngrok: a "Log in with GitHub" button that opens `/api/auth/github` (only when `github_login` is on), a line "or", then the email form (or the SMS form when `email_login` is off), and at the bottom small links to the other ways. When the address has `login_error` (AUTH-29) the card SHALL show its text and remove it from the address. The GitHub mark SHALL be inline.

#### Scenario: GitHub on
- **GIVEN** `github_login` is true
- **WHEN** the login screen opens
- **THEN** the first button is "Log in with GitHub" and links to `/api/auth/github`, and the email field is below it

#### Scenario: GitHub off
- **GIVEN** `github_login` is false
- **WHEN** the login screen opens
- **THEN** there is no GitHub button and no "or" line

#### Scenario: Error from GitHub
- **WHEN** the page opens at `/?login_error=github_unavailable`
- **THEN** the login card shows "GitHub cannot be reached now. Log in by email or SMS." in the person's language and the address becomes `/`

#### Scenario: Texts
- **WHEN** the error is `github_state`, `github_denied`, `github_no_email`, `github_unavailable` or `suspended`
- **THEN** it has its own text, in Persian and in English
