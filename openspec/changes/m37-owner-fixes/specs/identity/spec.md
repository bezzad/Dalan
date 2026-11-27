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


### Requirement: AUTH-32 The account with products is kept
Products SHALL be bots not deleted, sites, demos and access keys. When one account has products and the other has none, the account with products SHALL be kept; when neither has, the current session's; an admin's account (SEC-14) SHALL always be the kept one. When both have products, the merge SHALL be refused with 409 `both_have_products`. A merge SHALL NOT change, move or delete any product.

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

