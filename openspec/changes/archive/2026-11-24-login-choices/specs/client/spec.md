# Spec Delta

## MODIFIED Requirements

### Requirement: CLI-36 One simple login card
The login screen SHALL be one card with one equal button per way to log in: "Log in with SMS", "Log in with GitHub" (opens `/api/auth/github`, only when `github_login` is on) and "Log in with email" (only when `email_login` is on), each with its inline mark. A browser that never logged in (no `dalan.returning` in local storage) SHALL get the order SMS, GitHub, email; after any login the client SHALL set `dalan.returning`, and the order SHALL be GitHub, email, SMS. The SMS and email buttons SHALL open their form with a "back" button to the list. Password and the development login SHALL stay small links at the bottom. When the address has `login_error` (AUTH-29) the card SHALL show its text and remove it from the address.

#### Scenario: New browser
- **GIVEN** `github_login` and `email_login` are true and local storage has no `dalan.returning`
- **WHEN** the login screen opens
- **THEN** the buttons are, in order, SMS, GitHub, email

#### Scenario: Returning browser
- **GIVEN** the person logged in before on this browser
- **WHEN** the login screen opens
- **THEN** the buttons are, in order, GitHub, email, SMS

#### Scenario: GitHub on
- **GIVEN** `github_login` is true
- **WHEN** the login screen opens
- **THEN** a "Log in with GitHub" button links to `/api/auth/github`

#### Scenario: GitHub off
- **GIVEN** `github_login` is false
- **WHEN** the login screen opens
- **THEN** there is no GitHub button

#### Scenario: Error from GitHub
- **WHEN** the page opens at `/?login_error=github_unavailable`
- **THEN** the login card shows "GitHub cannot be reached now. Log in by email or SMS." in the person's language and the address becomes `/`

#### Scenario: Texts
- **WHEN** the error is `github_state`, `github_denied`, `github_no_email`, `github_unavailable` or `suspended`
- **THEN** it has its own text, in Persian and in English

## ADDED Requirements

### Requirement: CLI-61 Send the code again
The code screen SHALL have a "Send again" button, off with a countdown (`m:ss`) for `retry_in` seconds of the sent code, then on. Pressing it SHALL ask a new code for the same phone or email (with a new proof, CLI-10) and start the countdown again. A 429 with `Retry-After` on asking a code SHALL show "Try again in m:ss" with the seconds of the header. No text SHALL send the person to "tomorrow".

#### Scenario: Countdown
- **GIVEN** a code was sent with `retry_in` 120
- **WHEN** the code screen opens
- **THEN** "Send again" is off and shows `2:00`, and it is on after 120 seconds

#### Scenario: Too soon
- **WHEN** asking a code gets 429 `sms_wait` with `Retry-After: 1680`
- **THEN** the screen shows "Try again in 28:00"
