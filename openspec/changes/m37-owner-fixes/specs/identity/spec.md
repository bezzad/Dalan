## MODIFIED Requirements

### Requirement: AUTH-39 A verified phone makes a builder
With `Identity:AutoBuilder` on (start true), a user with a verified phone SHALL be a builder (`approved`) with no request or approval (owner, 2026-11-27): at an SMS login, when the phone is added (AUTH-26), after a merge (AUTH-33), and once for every such user when this change is installed, whatever the state was, except `refused` by an admin. `POST /api/account/builder` SHALL stay for old clients and do the same. A user with no phone SHALL still get 403 `phone_required`.

#### Scenario: New SMS user
- **WHEN** a new user logs in by SMS
- **THEN** `GET /api/me` says `builder` `approved`

#### Scenario: Old states
- **GIVEN** users with a phone in the states `none`, `requested` and `refused`
- **WHEN** this change is installed
- **THEN** the first two are `approved`, and the refused one stays `refused`

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


### Requirement: AUTH-19 Builder request
`POST /api/account/builder` SHALL set the user's builder state, when `Identity:AutoBuilder` is on, from `none` and `requested` to `approved`, and `refused` stays (AUTH-39: a request would be approved at the next SMS login); when it is off, from `none` and `refused` to `requested`. `approved` stays. It answers the profile. A user with no phone (level 1) SHALL get 403 `phone_required` and nothing changes. `GET /api/me` SHALL say `builder`: `none`, `requested`, `approved` or `refused`. An admin's approval SHALL set `approved`, a refusal `refused`. Admins SHALL count as builders.

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
- **THEN** `GET /api/me` says `builder` `refused`; with `Identity:AutoBuilder` off a new request waits for the admin, with it on the state stays `refused`


### Requirement: AUTH-32 The account with products is kept
Products SHALL be bots not deleted, sites, demos and access keys. When one account has products and the other has none, the account with products SHALL be kept; when neither has, the current session's; an admin's account (SEC-14) SHALL always be kept, and refused with 409 `other_has_products` when the other has products. When both have products: 409 `both_have_products`. A merge SHALL NOT change, move or delete any product.

#### Scenario: The other account has bots
- **GIVEN** Sara's phone account has no products and her email account has a bot
- **WHEN** she merges them
- **THEN** the email account is kept with its bot and gets the phone

#### Scenario: Admin
- **GIVEN** the admin's phone account and an email account of the same person with no products
- **WHEN** the merge is done with the admin's TOTP code (SEC-15)
- **THEN** the admin's account is kept and gets the email; the email account is deleted

#### Scenario: Neither has products
- **WHEN** two accounts with no products are merged
- **THEN** the account of the current session is kept

#### Scenario: Both have products
- **GIVEN** each account has a bot
- **WHEN** the user posts `/api/account/merge`
- **THEN** the answer is 409 `both_have_products` and nothing changes

