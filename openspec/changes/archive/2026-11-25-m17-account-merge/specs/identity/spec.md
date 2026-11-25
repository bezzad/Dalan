## MODIFIED Requirements

### Requirement: AUTH-26 Add a phone number (level 2)
With a session, `POST /api/account/phone` with `phone` and `code` SHALL check the SMS code (asked with `POST /api/auth/sms`, rules of AUTH-12). A right code SHALL save a free phone on the user (200 with the profile, `has_phone` true), or make a pending merge (AUTH-31, 409 `merge_needed`) for a phone of another account. A wrong code SHALL get 400 `wrong_code`; a user who has a phone, 409 `has_phone`. No access key.

#### Scenario: Add a phone
- **GIVEN** an email user with no phone, and an SMS code sent to `09121234567`
- **WHEN** the user posts the number and the code
- **THEN** the answer is 200 with `has_phone` true, and the user can now log in by SMS code with that number

#### Scenario: Wrong code
- **WHEN** the user posts a wrong code
- **THEN** the answer is 400 `wrong_code` and the user still has no phone

#### Scenario: Phone of another user
- **GIVEN** another account has the number
- **WHEN** the user posts the number and the right code
- **THEN** the answer is 409 `merge_needed`, a pending merge exists, and nothing else has changed

#### Scenario: Already has a phone
- **GIVEN** a user with a phone
- **WHEN** the user posts another number and its right code
- **THEN** the answer is 409 `has_phone`

## ADDED Requirements

### Requirement: AUTH-30 Link an email or GitHub
With a session, `POST /api/account/email` (`email`, `code` from `POST /api/auth/email`) and `GET /api/account/github` (the GitHub login of AUTH-27, whose callback links instead of logging in) SHALL save a free email or GitHub account on the user. One of another account SHALL make a pending merge (AUTH-31). A way the user already has SHALL be refused. No access key.

#### Scenario: Link a free email
- **GIVEN** a phone user with no email, and an email code sent to `sara@example.com`
- **WHEN** the user posts the address and the right code
- **THEN** the answer is 200 and the user can now log in by email code with that address

#### Scenario: Link GitHub
- **GIVEN** a phone user with no GitHub id, and GitHub account 42 that no Dalan user has
- **WHEN** the user links GitHub and GitHub comes back
- **THEN** the answer is 302 to `/account`, the user has GitHub id 42, and the session is the same user

#### Scenario: Email of another account
- **GIVEN** another account has `sara@example.com`
- **WHEN** the user posts it with the right code
- **THEN** the answer is 409 `merge_needed` and a pending merge exists

#### Scenario: GitHub of another account
- **GIVEN** another account has GitHub id 42
- **WHEN** the user links GitHub and GitHub account 42 comes back
- **THEN** the answer is 302 to `/account?merge=1` and a pending merge exists

#### Scenario: Already has an email or GitHub
- **GIVEN** a user with an email and a GitHub id
- **WHEN** the user posts another email, or opens `/api/account/github`
- **THEN** the answer is 409 `has_email`, or a redirect to `/account?link_error=has_github`

### Requirement: AUTH-31 Proof first, then the merge question
A pending merge SHALL be made only after the proof that the person owns the other account's way (right SMS code, right email code, or the GitHub callback); without the proof no caller SHALL learn that a way belongs to another account. A session SHALL have at most one pending merge, which ends after `Identity:MergeSeconds` (start 600). `GET /api/account/merge` SHALL show it (404 when none), `POST` SHALL do it and `DELETE` SHALL drop it.

#### Scenario: No proof, no answer
- **GIVEN** another account has `09121234567`
- **WHEN** the user posts the number with a wrong code
- **THEN** the answer is 400 `wrong_code`, the same as for a free number, and no pending merge exists

#### Scenario: The question
- **GIVEN** a pending merge with an account that has 2 bots
- **WHEN** the user asks `GET /api/account/merge`
- **THEN** the answer has the way, which account is kept (AUTH-32), the bots, sites and chats of each side, and any refusal (AUTH-32, AUTH-33)

#### Scenario: A new proof replaces it
- **GIVEN** a pending merge for a phone
- **WHEN** the user proves an email of a third account
- **THEN** the only pending merge is the one for the email

#### Scenario: Cancel
- **WHEN** the user sends `DELETE /api/account/merge`
- **THEN** `GET /api/account/merge` answers 404 and both accounts are unchanged

#### Scenario: Expired
- **GIVEN** a pending merge older than `Identity:MergeSeconds`
- **WHEN** the user posts `/api/account/merge`
- **THEN** the answer is 404 and nothing changes

### Requirement: AUTH-32 The account with products is kept
Products SHALL be bots not deleted, sites, demos and access keys. When one account has products and the other has none, the account with products SHALL be kept. When neither has products, the account of the current session SHALL be kept. When both have products, the merge SHALL be refused with 409 `both_have_products`. A merge SHALL NOT change, move or delete any product.

#### Scenario: The other account has bots
- **GIVEN** the current email account has no products, and the phone account has 2 bots
- **WHEN** the merge is done
- **THEN** the phone account is kept, with its 2 bots unchanged, and now has the email too

#### Scenario: Neither has products
- **WHEN** two accounts with no products are merged
- **THEN** the account of the current session is kept

#### Scenario: Both have products
- **GIVEN** each account has a bot
- **WHEN** the user posts `/api/account/merge`
- **THEN** the answer is 409 `both_have_products` and nothing changes

### Requirement: AUTH-33 Only empty fields are taken
The kept account SHALL take the phone, email, GitHub id, password and username of the other account only where its own is empty, and the higher builder state (`Approved` over `Requested` over `None`). When both accounts have a different phone, a different email or a different GitHub id, the merge SHALL be refused with 409 `merge_conflict` and the field. The display name of the kept account SHALL stay.

#### Scenario: Fields
- **GIVEN** the kept account has a phone and no email, and the other has an email and a password
- **WHEN** the merge is done
- **THEN** the kept account has its phone, the email and the password

#### Scenario: Two emails
- **GIVEN** both accounts have a different email
- **WHEN** the user posts `/api/account/merge`
- **THEN** the answer is 409 `merge_conflict` with field `email` and nothing changes

### Requirement: AUTH-34 After the merge
After a merge the other account SHALL be deleted with its sessions, and the person SHALL be logged in as the kept account (a new session when the kept account is not the current one). The audit log SHALL get `user {from} merged into user {into}` with the way. Rows of the access log (ADM-01) SHALL NOT be changed. A `UserMerged` event SHALL go to the outbox for the other services (REL-55).

#### Scenario: Logged in as the kept account
- **GIVEN** the current account is merged into the phone account that has bots
- **WHEN** the merge is done
- **THEN** the answer is 200 with new session cookies of the phone account, and the old account and its sessions are gone

#### Scenario: Logs
- **WHEN** user 9 is merged into user 4
- **THEN** the audit log has `user 9 merged into user 4`, and the access log rows of user 9 are unchanged
