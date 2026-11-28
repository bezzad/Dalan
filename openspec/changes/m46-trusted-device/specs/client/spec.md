## MODIFIED Requirements

### Requirement: CLI-92 Second factor in the app
"Login and security" (CLI-77) SHALL have a second-factor card: Set up shows the QR (from the server), the secret to copy and a code box; a right code shows the recovery codes once with Copy and Download. When a login answers `totp_required`, the login screen SHALL ask for the code (or a recovery code) and then finish the login; the step SHALL have a checkbox "Don't ask for the code on this device", off at first, sent as `trust` (AUTH-44). While the second factor is on, the card SHALL list the trusted devices with Forget for each and "Forget all". For an admin the card SHALL have no Turn off.

#### Scenario: Set up
- **WHEN** the user presses Set up and types the code from the authenticator app
- **THEN** the card says it is on, and the ten recovery codes show once

#### Scenario: Login step
- **GIVEN** a password login answered `totp_required`
- **WHEN** the user types a right code
- **THEN** the login finishes as usual

#### Scenario: Trust this device
- **GIVEN** the login step
- **WHEN** the user ticks "Don't ask for the code on this device" and sends a right code
- **THEN** the call has `trust` true; without the tick it has `trust` false
