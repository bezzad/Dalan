## MODIFIED Requirements

### Requirement: SEC-15 Second factor for admins
An admin (SEC-14) SHALL have the second factor (AUTH-37) on. Every login of an admin, by password, SMS, email or GitHub, SHALL also need a TOTP or an unused recovery code before a session is made; so SHALL a merge (AUTH-31) where one side is an admin: `POST /api/account/merge` with `code`, else 403 `admin_merge`. While an admin has no second factor, every `/api/admin` call SHALL get 403 `admin_needs_2fa`, and the dashboard SHALL show only its set-up. It SHALL NOT be turned off.

#### Scenario: Admin logs in by SMS
- **GIVEN** an admin with the second factor on
- **WHEN** the admin enters the right SMS code
- **THEN** the answer asks for the TOTP code (`totp_required`) and no session is made until it is right

#### Scenario: Merge with an admin
- **GIVEN** a pending merge between the admin's account and another account
- **WHEN** the merge is sent without a code, then with a wrong code, then with the admin's right TOTP code
- **THEN** the first two get 403 `admin_merge` and change nothing; the third merges, keeping the admin's account

#### Scenario: No second factor yet
- **GIVEN** an admin without the second factor
- **WHEN** the admin opens `/dashboard/admin` or calls `GET /api/admin/settings`
- **THEN** the page shows only the set-up of the second factor, and the call gets 403 `admin_needs_2fa`

#### Scenario: Cannot turn it off
- **WHEN** an admin asks to turn the second factor off
- **THEN** the answer is 403 and it stays on

#### Scenario: Admin powers outside /api/admin
- **GIVEN** an admin without the second factor
- **WHEN** the admin changes another user's bot or deletes another user's site
- **THEN** it is refused as for a common user; with the second factor on, it works

#### Scenario: Turning it on ends the other sessions
- **GIVEN** an admin session made on another device before the second factor
- **WHEN** the admin turns the second factor on
- **THEN** that other session ends; only this one stays

