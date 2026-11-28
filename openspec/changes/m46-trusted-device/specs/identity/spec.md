## ADDED Requirements

### Requirement: AUTH-44 Trust this device
`POST /api/auth/totp` with `"trust": true` and a right code SHALL also set the cookie `__Host-dalan_trusted` (HttpOnly, Secure, SameSite Lax, Path `/`, lasting `Identity:TrustedDeviceDays`): 256 random bits; only its SHA-256 is kept, with the user, browser, system, address and end time. A later login of the same user that would ask for the code (AUTH-37, SEC-15) SHALL make the session at once when this browser sends a live trusted token of that user. `GET /api/account/trusted-devices` SHALL list the user's live ones (id, browser, system, address, created, ends, `current`); `DELETE /api/account/trusted-devices/{id}` SHALL forget one and `DELETE /api/account/trusted-devices` all. Turning the second factor on or off SHALL forget all; setting the password and "Log out other devices" SHALL forget all but this browser's. `Identity:TrustedDeviceDays` 0 SHALL turn trust off: no new token, and old ones do not skip the code. Each forget SHALL be in the audit log.

#### Scenario: Trust
- **GIVEN** TOTP is on
- **WHEN** the user finishes a password login with a right code and `trust` true, then logs in with the password again on that browser
- **THEN** the first answer sets `__Host-dalan_trusted` (HttpOnly, Secure, SameSite Lax), the table has only its hash, and the second login makes the session with no `totp_required`

#### Scenario: Not trusted
- **WHEN** the code is sent without `trust`, or another browser logs in, or another user sends the token
- **THEN** no trusted cookie is set, and the login asks for the code

#### Scenario: Ends
- **WHEN** the token is older than `Identity:TrustedDeviceDays` (30)
- **THEN** the login asks for the code

#### Scenario: Forget
- **GIVEN** two trusted browsers
- **WHEN** the user lists them, then forgets one
- **THEN** the list has two with `current` true for the caller's; after the forget, that browser's login asks for the code

#### Scenario: Password or second factor changes
- **WHEN** the user sets the password, or turns the second factor off and on again
- **THEN** other browsers' trust is gone; after the second factor changes, this browser's too

#### Scenario: Off
- **GIVEN** `Identity:TrustedDeviceDays` is 0
- **WHEN** a trusted browser logs in, or a code comes with `trust` true
- **THEN** the login asks for the code, and no trusted cookie is set
