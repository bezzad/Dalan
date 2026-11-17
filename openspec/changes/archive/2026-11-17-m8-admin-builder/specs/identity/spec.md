## MODIFIED Requirements

### Requirement: AUTH-02 Other services learn only the public profile
Other services SHALL read a user only through the contract `IUserDirectory`, which gives the user's id, display name and username, and what the user may do (admin, builder, suspended). The contract SHALL NOT carry the phone number. Only the Admin service SHALL use `IUserAdmin`, which carries it for legal complaints (ADM-11).

#### Scenario: Known user
- **WHEN** a service asks for an existing user
- **THEN** it gets the id, display name and username

#### Scenario: Unknown user
- **WHEN** a service asks for a user id that does not exist
- **THEN** it gets nothing

#### Scenario: No phone in the contract
- **WHEN** the types of `Dalan.Identity.Contracts` other than `IUserAdmin` and `AdminUser` are inspected
- **THEN** none has a member with "phone" in its name

## ADDED Requirements

### Requirement: AUTH-18 Admins
A user SHALL be an admin when the user's phone number is in `Identity:AdminPhones` (comma separated, any Iranian form). The setting SHALL be read from the file or environment only (never from the panel). `GET /api/me` SHALL say `admin: true|false`. A change of the setting takes effect at once for the admin door.

#### Scenario: Admin phone
- **GIVEN** `Identity:AdminPhones` is `09121234567`
- **WHEN** the user with phone `+989121234567` calls `GET /api/me`
- **THEN** the answer has `admin` true

#### Scenario: Other user
- **WHEN** another user calls `GET /api/me`
- **THEN** the answer has `admin` false

### Requirement: AUTH-19 Builder request
`POST /api/account/builder` SHALL set the user's builder state to `requested`, unless it is already `approved` (then nothing changes); it answers the profile. `GET /api/me` SHALL say `builder`: `none`, `requested` or `approved`. An admin's approval SHALL set `approved`; a refusal SHALL set `none`, and the user may ask again. Admins SHALL count as builders.

#### Scenario: Ask
- **WHEN** a user asks to become a builder
- **THEN** `GET /api/me` says `builder` `requested`

#### Scenario: Approved
- **GIVEN** a user asked
- **WHEN** an admin approves
- **THEN** `GET /api/me` says `builder` `approved`, and the user can create a bot at once (REL-36)

#### Scenario: Refused
- **GIVEN** a user asked
- **WHEN** an admin refuses
- **THEN** `GET /api/me` says `builder` `none`

### Requirement: AUTH-20 Suspended user
When an admin suspends a user, the server SHALL delete all of the user's sessions, SHALL refuse every call with the user's session token at once with 401 `unauthorized` (even before the token expires), SHALL close the user's live streams (LIVE-01), and SHALL answer SMS and password login of that user with 403 `suspended` without sending an SMS. After unsuspend the user SHALL be able to log in again.

#### Scenario: Session stops at once
- **GIVEN** a logged-in user
- **WHEN** an admin suspends the user
- **THEN** the user's next `GET /api/me` and `POST /api/auth/refresh` get 401

#### Scenario: Live stream closes
- **GIVEN** a user has an open `GET /api/events`
- **WHEN** an admin suspends the user
- **THEN** the stream ends

#### Scenario: No login, no SMS
- **GIVEN** a suspended user
- **WHEN** someone asks a login code for the user's phone, or logs in with the user's password
- **THEN** the answer is 403 `suspended` and no SMS is sent

#### Scenario: Unsuspend
- **GIVEN** a suspended user
- **WHEN** an admin unsuspends the user
- **THEN** the user can log in with the password again
