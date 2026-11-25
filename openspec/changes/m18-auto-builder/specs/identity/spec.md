## MODIFIED Requirements

### Requirement: AUTH-19 Builder request
`POST /api/account/builder` SHALL set the user's builder state from `none` to `approved` when `Identity:AutoBuilder` is on, else to `requested`; from `refused` to `requested`; `requested` and `approved` stay. It answers the profile. A user with no phone (level 1) SHALL get 403 `phone_required` and nothing changes. `GET /api/me` SHALL say `builder`: `none`, `requested`, `approved` or `refused`. An admin's approval SHALL set `approved`, a refusal `refused`. Admins SHALL count as builders.

#### Scenario: Ask
- **GIVEN** `Identity:AutoBuilder` is off
- **WHEN** a user asks to become a builder
- **THEN** `GET /api/me` says `builder` `requested`

#### Scenario: Ask with auto builder
- **GIVEN** `Identity:AutoBuilder` is on and a user with a phone in state `none`
- **WHEN** the user asks to become a builder
- **THEN** `GET /api/me` says `builder` `approved`

#### Scenario: No phone
- **GIVEN** a user who logged in by email and has no phone
- **WHEN** the user asks to become a builder
- **THEN** the answer is 403 `phone_required` and `builder` stays `none`

#### Scenario: Approved
- **GIVEN** a user asked
- **WHEN** an admin approves
- **THEN** `GET /api/me` says `builder` `approved`, and the user can create a bot at once (REL-36)

#### Scenario: Refused
- **GIVEN** a user asked
- **WHEN** an admin refuses
- **THEN** `GET /api/me` says `builder` `refused`, and a new request waits for the admin even with `Identity:AutoBuilder` on

## ADDED Requirements

### Requirement: AUTH-35 A verified phone makes a builder
When `Identity:AutoBuilder` is on (start true), a user with a verified phone and builder state `none` SHALL become `approved` at an SMS login, when the phone is added (AUTH-26) and after a merge (AUTH-33). `requested` and `refused` SHALL NOT change. The requests open when this change is installed SHALL be approved once.

#### Scenario: New SMS user
- **GIVEN** `Identity:AutoBuilder` is on
- **WHEN** a new person logs in by SMS code
- **THEN** `GET /api/me` says `builder` `approved`

#### Scenario: Phone added
- **GIVEN** `Identity:AutoBuilder` is on and an email user with no phone
- **WHEN** the user adds a phone (AUTH-26)
- **THEN** `GET /api/me` says `builder` `approved`

#### Scenario: Refused stays refused
- **GIVEN** `Identity:AutoBuilder` is on and a refused user
- **WHEN** the user logs in by SMS code
- **THEN** `GET /api/me` says `builder` `refused`

#### Scenario: Off
- **GIVEN** `Identity:AutoBuilder` is off
- **WHEN** a new person logs in by SMS code
- **THEN** `GET /api/me` says `builder` `none`
