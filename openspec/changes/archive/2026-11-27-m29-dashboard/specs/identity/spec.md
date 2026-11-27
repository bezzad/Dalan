## ADDED Requirements

### Requirement: AUTH-37 Second factor
A user with a password, or an admin, SHALL be able to turn on TOTP: `POST /api/account/totp` gives a secret, an `otpauth://` address and its QR as SVG; the first right code turns it on and gives `Identity:RecoveryCodes` one-time recovery codes, shown once and kept hashed. Then a password login SHALL also need a TOTP code or an unused recovery code. Turning it off SHALL need a code. SMS, email and GitHub logins of a common user SHALL NOT ask for it.

#### Scenario: Turn on
- **WHEN** the user sends the first right code
- **THEN** TOTP is on and ten recovery codes come back once

#### Scenario: Password login
- **GIVEN** TOTP is on
- **WHEN** the user logs in with the right password and no code
- **THEN** the answer is `{"totp_required": true, "ticket": ...}` with no session, and `POST /api/auth/totp` with the ticket and a right code makes the session

#### Scenario: Code window
- **WHEN** a code of the step before or after now comes (`Identity:TotpStepSeconds` 30, `Identity:TotpWindowSteps` 1)
- **THEN** it is taken; a code used once is refused the second time

#### Scenario: Recovery code
- **WHEN** the user sends an unused recovery code instead of a TOTP code
- **THEN** the login goes on and that recovery code cannot be used again

#### Scenario: Ticket
- **WHEN** a ticket older than `Identity:TotpTicketMinutes` (5) comes, or a wrong code comes `Identity:TotpMaxTries` (5) times
- **THEN** the answer is 401 and the user logs in again

#### Scenario: Turn off
- **WHEN** a common user sends a right code to `POST /api/account/totp/off`
- **THEN** TOTP is off and the recovery codes are gone

#### Scenario: Guessing the code to turn it off
- **WHEN** wrong codes come to `POST /api/account/totp/off` `Identity:PasswordTries` times in its window
- **THEN** the next try, even with a right code, gets 429 `rate_limited`

#### Scenario: Code step stays
- **WHEN** an admin tries to change `Identity:TotpStepSeconds` in the panel
- **THEN** the answer is 400 `out_of_range`: the phones keep the step of their set-up
