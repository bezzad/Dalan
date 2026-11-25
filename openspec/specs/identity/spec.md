# identity Specification

## Purpose
Who the users are, and the little that other services and bots may learn about them.

## Requirements

### Requirement: AUTH-01 Identity owns the users
Identity SHALL keep users in the PostgreSQL schema `identity`. A user has a numeric id that never changes, a phone number or an email address or both, a display name, an optional username and an optional GitHub account id. A phone number SHALL belong to one user only, an email address SHALL belong to one user only (it is stored in lower case), a GitHub account id SHALL belong to one user only, and a username SHALL be unique without regard to letter case.

#### Scenario: Schema after start
- **WHEN** the server has started on an empty database
- **THEN** the table `identity.users` exists

#### Scenario: Same phone number twice
- **GIVEN** a user with phone number `+989121234567`
- **WHEN** a second user with the same number is saved
- **THEN** the save fails

#### Scenario: Same email twice
- **GIVEN** a user with email `sara@example.com`
- **WHEN** a second user with the same email is saved
- **THEN** the save fails

#### Scenario: Same GitHub account twice
- **GIVEN** a user with GitHub account id `42`
- **WHEN** a second user with the same GitHub account id is saved
- **THEN** the save fails

#### Scenario: Same username in another case
- **GIVEN** a user with username `Sara`
- **WHEN** a second user with username `sara` is saved
- **THEN** the save fails

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

### Requirement: AUTH-03 Session cookie
A login SHALL give the browser a cookie named `__Host-dalan_session` with the flags `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/` and a `Max-Age` equal to the token lifetime. Its value SHALL be a JWT signed with ES256 that carries the user id and expires after `Identity:AccessTokenMinutes` (start 15). A missing, expired, changed or wrongly signed token SHALL NOT be accepted.

#### Scenario: Cookie flags
- **WHEN** a user logs in
- **THEN** the response sets `__Host-dalan_session` with `HttpOnly`, `Secure`, `SameSite=Strict` and `Path=/`

#### Scenario: Token is ES256
- **WHEN** a user logs in
- **THEN** the cookie value is a JWT whose header says `ES256` and whose `sub` is the user id

#### Scenario: Expired
- **GIVEN** a logged-in user whose refresh cookie is gone
- **WHEN** the lifetime passes and the user calls `GET /api/me`
- **THEN** the response is 401 `unauthorized`

#### Scenario: Changed token
- **WHEN** a call carries a session cookie whose user id was changed
- **THEN** the response is 401 `unauthorized`

### Requirement: AUTH-04 Who am I
`GET /api/me` SHALL return the `id`, `name`, `username`, `email`, `has_password` and `has_phone` of the logged-in user, and never the phone number or the password hash. Without a session it SHALL answer 401 `unauthorized`.

#### Scenario: Logged in
- **WHEN** a logged-in user calls `GET /api/me`
- **THEN** the response is 200 with the user's id and name, and the body does not contain the phone number

#### Scenario: Not logged in
- **WHEN** a call to `GET /api/me` has no session cookie
- **THEN** the response is 401 `unauthorized`

#### Scenario: Has a password
- **GIVEN** a user who set a password
- **WHEN** the user calls `GET /api/me`
- **THEN** `has_password` is true, and the body holds no hash

#### Scenario: Email user
- **GIVEN** a user who logged in by email code and has no phone
- **WHEN** the user calls `GET /api/me`
- **THEN** `email` is the address and `has_phone` is false

### Requirement: AUTH-05 Logout
`POST /api/logout` SHALL remove the session cookie and the refresh cookie from the browser and SHALL end this device's session on the server, so its refresh token stops working. Other devices of the user stay logged in.

#### Scenario: Log out
- **GIVEN** a logged-in user
- **WHEN** the user calls `POST /api/logout` and then `GET /api/me`
- **THEN** the second response is 401

#### Scenario: Refresh token is dead after logout
- **GIVEN** a user who kept a copy of the refresh token and logged out
- **WHEN** the copy is sent to `POST /api/auth/refresh`
- **THEN** the response is 401 `unauthorized`

#### Scenario: Other device stays
- **GIVEN** a user logged in on two devices
- **WHEN** the first device logs out
- **THEN** the second device can still refresh

### Requirement: AUTH-06 Development login is only for development
`POST /api/dev/login` with `phone` (an Iranian mobile number like `09121234567`) and `name` SHALL find or create the user with that number and start a session, with no SMS. It SHALL exist only when the setting `Identity:DevLogin` is on (it is off by default) and the server runs in the Development environment. With the setting on in any other environment the server SHALL refuse to start.

#### Scenario: Setting is off
- **WHEN** `Identity:DevLogin` is off and a client calls `POST /api/dev/login`
- **THEN** the response is 404

#### Scenario: Setting is on outside Development
- **WHEN** the server starts in the Production environment with `Identity:DevLogin` on
- **THEN** it does not start, and the error says why

#### Scenario: New number
- **GIVEN** development login is on
- **WHEN** a client posts `{"phone":"09121234567","name":"Sara"}`
- **THEN** a user with phone `+989121234567` and name "Sara" exists and the response sets the session cookie

#### Scenario: Same number again
- **GIVEN** a user logged in with a number
- **WHEN** a client logs in with the same number again
- **THEN** the same user id is used and no second user is stored

#### Scenario: Not a mobile number
- **WHEN** a client posts a phone that is not an Iranian mobile number, or no name
- **THEN** the response is 400 with error code `bad_request`

### Requirement: AUTH-07 Signing key comes from settings
The key that signs sessions SHALL come from the secret setting `Identity:SigningKey` (a P-256 private key, PKCS#8, base64). Without it the server SHALL make a temporary key at start and log a warning; sessions then end when the server restarts.

#### Scenario: Same key, two servers
- **GIVEN** two server instances with the same `Identity:SigningKey`
- **WHEN** a session made by the first is sent to the second
- **THEN** it is accepted

#### Scenario: No key
- **GIVEN** two server instances without `Identity:SigningKey`
- **WHEN** a session made by the first is sent to the second
- **THEN** the response is 401

### Requirement: AUTH-08 Refresh cookie
Every login (SMS code, password, development) SHALL also set a cookie named `__Host-dalan_refresh` with the flags `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/` and a `Max-Age` of `Identity:RefreshTokenDays` (start 30). Its value SHALL be a random token of at least 256 bits. The server SHALL store only a SHA-256 hash of it, in `identity.sessions`, one row per logged-in device.

#### Scenario: Cookie flags
- **WHEN** a user logs in
- **THEN** the response sets `__Host-dalan_refresh` with `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/` and `Max-Age` of 30 days

#### Scenario: Stored hashed
- **WHEN** a user logs in
- **THEN** `identity.sessions` has one row for the user, and the token itself is in no column

### Requirement: AUTH-09 Refresh replaces the token
`POST /api/auth/refresh` with a valid refresh cookie SHALL set a new session cookie and a new refresh cookie, and the old refresh token SHALL stop working. The new refresh token lives `Identity:RefreshTokenDays` from that moment. A missing, unknown or expired refresh token, or one of a user that no longer exists, SHALL get 401 `unauthorized`.

#### Scenario: Session goes on after the access token ends
- **GIVEN** a logged-in user whose access token has expired
- **WHEN** the browser calls `POST /api/auth/refresh` and then `GET /api/me`
- **THEN** both succeed and the user id is the same

#### Scenario: Token is replaced
- **WHEN** a refresh succeeds
- **THEN** the refresh cookie has a new value and the database row holds the hash of the new one

#### Scenario: No cookie
- **WHEN** `POST /api/auth/refresh` is called without a refresh cookie
- **THEN** the response is 401 `unauthorized`

#### Scenario: Expired
- **GIVEN** a logged-in user
- **WHEN** `Identity:RefreshTokenDays` pass with no refresh, and then the browser calls `POST /api/auth/refresh`
- **THEN** the response is 401 `unauthorized`

#### Scenario: Each refresh adds life
- **GIVEN** a user who refreshed on day 20
- **WHEN** the browser refreshes again on day 45
- **THEN** it succeeds

### Requirement: AUTH-10 Reuse of an old refresh token ends that session
When a refresh token that was already replaced is sent again later than `Identity:RefreshReuseGraceSeconds` (start 10) after it was replaced, the server SHALL delete that device's session, so the newest token stops working too, and SHALL answer 401. Inside the grace time the server SHALL answer 401 and keep the session, because two tabs can refresh at the same moment.

#### Scenario: Stolen token used later
- **GIVEN** a refresh token A that was replaced by B more than the grace time ago
- **WHEN** A is sent to `POST /api/auth/refresh`
- **THEN** the response is 401, and a refresh with B is 401 too

#### Scenario: Two tabs at the same moment
- **GIVEN** a refresh token A that was replaced by B a moment ago
- **WHEN** A is sent again inside the grace time
- **THEN** the response is 401, and a refresh with B still succeeds

#### Scenario: Other devices are not touched
- **GIVEN** a user with sessions on two devices
- **WHEN** an old token of the first device is reused after the grace time
- **THEN** the second device can still refresh

### Requirement: AUTH-11 Ask for an SMS code
`POST /api/auth/sms` with `phone`, `challenge` and `nonce` (SEC-06) SHALL make a random code of `Identity:SmsCodeDigits` digits (start 6), store only a keyed hash of it, send it through `ISmsSender` (NTF-01), and answer 200 with `expires_in`, `retry_in` and `code_digits`, the same for a known and an unknown number. A phone that is not an Iranian mobile number SHALL get 400 `bad_phone`. A failed send SHALL get 503 `sms_unavailable` and SHALL NOT count toward any limit.

#### Scenario: Code is sent
- **WHEN** a client asks a code for `09121234567` with a valid proof
- **THEN** one SMS with a 6-digit code goes to `+989121234567` and the response is 200 with `expires_in` 120 and `retry_in` 60

#### Scenario: Code is not stored in clear
- **WHEN** a code was sent
- **THEN** no column of `identity.sms_codes` holds the code

#### Scenario: Not a mobile number
- **WHEN** a client asks a code for `02112345678`
- **THEN** the response is 400 `bad_phone` and no SMS is sent

#### Scenario: Sender fails
- **GIVEN** the SMS sender fails
- **WHEN** a client asks a code
- **THEN** the response is 503 `sms_unavailable`, and a new try right after it is not refused by the limit per minute

### Requirement: AUTH-12 Log in with the SMS code
`POST /api/auth/sms/verify` with `phone` and `code` SHALL log the user of that number in when the code is the newest one sent to the number, is younger than `Identity:SmsCodeSeconds` (start 120), was not used, and had fewer than `Identity:SmsCodeTries` (start 5) wrong tries. Any other case SHALL get 400 `wrong_code`. A number with no user yet SHALL need a `name`: without it the answer SHALL be 400 `name_required` and the code SHALL stay valid; with it a new user SHALL be made.

#### Scenario: Known user
- **GIVEN** a user with the number and a code that was just sent
- **WHEN** the client posts the number and the code
- **THEN** the response is 200 with the user's profile and sets both cookies

#### Scenario: Wrong code
- **WHEN** the client posts a wrong code
- **THEN** the response is 400 `wrong_code` and no cookie is set

#### Scenario: Expired code
- **GIVEN** a code sent more than `Identity:SmsCodeSeconds` ago
- **WHEN** the client posts it
- **THEN** the response is 400 `wrong_code`

#### Scenario: Too many tries
- **GIVEN** 5 wrong tries on a code
- **WHEN** the client posts the right code
- **THEN** the response is 400 `wrong_code`

#### Scenario: Used once
- **GIVEN** a code that logged the user in
- **WHEN** the same code is posted again
- **THEN** the response is 400 `wrong_code`

#### Scenario: Only the newest code
- **GIVEN** two codes were sent to a number
- **WHEN** the client posts the first one
- **THEN** the response is 400 `wrong_code`

#### Scenario: New number without a name
- **GIVEN** a number with no user
- **WHEN** the client posts the right code without a name
- **THEN** the response is 400 `name_required` and no user is made

#### Scenario: New number with a name
- **GIVEN** a number with no user, and the answer `name_required`
- **WHEN** the client posts the same code again with the name "Sara"
- **THEN** a user "Sara" with that number exists and the response sets both cookies

#### Scenario: Known user keeps the name
- **GIVEN** a user named "Sara"
- **WHEN** the user logs in by code and the call carries the name "Other"
- **THEN** the user's name is still "Sara"

### Requirement: AUTH-13 Set a password
`POST /api/account/password` with `password` SHALL store an Argon2id hash (PHC text, own salt and cost) and answer 204. A password shorter than `Identity:PasswordMinLength` (start 8) or longer than `Identity:PasswordMaxLength` (start 128) SHALL get 400 `weak_password`. When the user has a password and the session did not start with an SMS or email code, the right `current_password` is needed, else 403 `wrong_password`. A change SHALL end every other session of the user.

#### Scenario: First password
- **GIVEN** a logged-in user with no password
- **WHEN** the user posts a password of 8 characters
- **THEN** the response is 204 and `identity.users.password_hash` starts with `$argon2id$`

#### Scenario: Too short
- **WHEN** the user posts a password of 7 characters
- **THEN** the response is 400 `weak_password`

#### Scenario: Change needs the current password
- **GIVEN** a user logged in by password
- **WHEN** the user posts a new password without `current_password`, or with a wrong one
- **THEN** the response is 403 `wrong_password` and the old password still works

#### Scenario: Change with the current password
- **GIVEN** a user logged in by password
- **WHEN** the user posts a new password with the right `current_password`
- **THEN** the response is 204 and only the new password works

#### Scenario: Forgot the password
- **GIVEN** a user with a password, logged in by SMS code
- **WHEN** the user posts a new password without `current_password`
- **THEN** the response is 204

#### Scenario: Forgot the password, email user
- **GIVEN** a user with a password, logged in by email code
- **WHEN** the user posts a new password without `current_password`
- **THEN** the response is 204

#### Scenario: Other devices are logged out
- **GIVEN** a user logged in on two devices
- **WHEN** the first device changes the password
- **THEN** the first device stays logged in and a refresh on the second device is 401

#### Scenario: Not logged in
- **WHEN** the call has no session
- **THEN** the response is 401 `unauthorized`

### Requirement: AUTH-14 Log in with a password
`POST /api/auth/password` with `login` (the phone number, the email address or the username) and `password` SHALL log the user in (AUTH-03, AUTH-08) when the password is right. An unknown login, a user with no password and a wrong password SHALL all get the same answer: 401 `wrong_login`.

#### Scenario: By phone
- **GIVEN** a user with phone `+989121234567` and a password
- **WHEN** the client posts `{"login":"09121234567","password":"<right>"}`
- **THEN** the response is 200 with the user's profile and sets both cookies

#### Scenario: By email, any letter case
- **GIVEN** a user with email `sara@example.com` and a password
- **WHEN** the client posts the login `Sara@Example.com` with the right password
- **THEN** the response is 200

#### Scenario: By username, any letter case
- **GIVEN** a user with username `Sara_1` and a password
- **WHEN** the client posts the login `sara_1` with the right password
- **THEN** the response is 200

#### Scenario: Wrong password
- **WHEN** the client posts a wrong password
- **THEN** the response is 401 `wrong_login` and no cookie is set

#### Scenario: Unknown login and no password look the same
- **WHEN** the client posts an unknown login, or the login of a user who never set a password
- **THEN** the response is 401 `wrong_login`

### Requirement: AUTH-15 Set a username
`POST /api/account/username` with `username` SHALL set the user's username and answer 200 with the profile. A username SHALL be 5 to 32 characters of `a-z`, `A-Z`, `0-9` and `_`, and start with a letter; anything else SHALL get 400 `bad_username`. A username another user has, in any letter case, SHALL get 409 `username_taken`.

#### Scenario: Set
- **WHEN** a logged-in user posts the username `Sara_1`
- **THEN** the response is 200 and `GET /api/me` shows `Sara_1`

#### Scenario: Bad shape
- **WHEN** the user posts `ab`, `1sara`, or `sara!`
- **THEN** the response is 400 `bad_username`

#### Scenario: Taken
- **GIVEN** another user has the username `Sara_1`
- **WHEN** the user posts `sara_1`
- **THEN** the response is 409 `username_taken`

### Requirement: AUTH-16 Login settings for the client
`GET /api/auth/settings` SHALL need no session and SHALL return `dev_login` (is the development login on), `email_login` (is an email provider set, NTF-10), `github_login` (are `Identity:GitHub:ClientId` and `Identity:GitHub:ClientSecret` set, AUTH-27), `password_min_length` and `name_max_length`, so the client hard-codes none of them.

#### Scenario: Read
- **WHEN** a client with no session calls `GET /api/auth/settings`
- **THEN** the response is 200 with `dev_login`, `email_login`, `github_login`, `password_min_length` 8 and `name_max_length` 64

#### Scenario: GitHub on
- **GIVEN** `Identity:GitHub:ClientId` and `Identity:GitHub:ClientSecret` are set
- **WHEN** a client calls `GET /api/auth/settings`
- **THEN** `github_login` is true; with either one empty it is false

### Requirement: AUTH-17 Security events are logged
The server SHALL write a log line for each login, failed login, sent SMS or email code, refused SMS or email code, reuse of an old refresh token, password change and added phone, with the event name and the user id when it is known. No log line SHALL hold a login code, a password, a token, a whole phone number or a whole email address. The only exceptions are the `log` senders (NTF-01, NTF-10), which exist for tests.

#### Scenario: Login is logged
- **WHEN** a user logs in by SMS code
- **THEN** the log has a line with the event `login` and the user id

#### Scenario: Failed login is logged
- **WHEN** a client posts a wrong password
- **THEN** the log has a line with the event `login_failed`

#### Scenario: No secret in the log
- **WHEN** a user asks a code, logs in with it and sets a password
- **THEN** no log line holds the code, the password or the whole phone number

#### Scenario: No email in the log
- **WHEN** a user asks an email code and logs in with it, with a real email sender
- **THEN** no log line holds the code or the whole email address

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
`POST /api/account/builder` SHALL set the user's builder state to `requested`, unless it is already `approved` (then nothing changes); it answers the profile. A user with no phone (level 1) SHALL get 403 `phone_required` and nothing changes. `GET /api/me` SHALL say `builder`: `none`, `requested` or `approved`. An admin's approval SHALL set `approved`; a refusal SHALL set `none`, and the user may ask again. Admins SHALL count as builders.

#### Scenario: Ask
- **WHEN** a user asks to become a builder
- **THEN** `GET /api/me` says `builder` `requested`

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

### Requirement: AUTH-21 Make an access key
With a session, `POST /api/account/keys` with a `name` of 1 to 64 characters SHALL answer 201 with `id`, `name`, `key`, `created_at` and `expires_at`, else 400 `bad_request`. The key SHALL be `dalan_` and 43 base64url characters, shown only here; only its SHA-256 hash is stored. It expires after `Identity:AccessKeyDays` (start 90) days. At most `Identity:MaxAccessKeys` (start 10) per user, else 409 `key_limit`. The audit log gets `key.create`.

#### Scenario: Make a key
- **WHEN** a logged-in user makes a key named `laptop`
- **THEN** the answer is 201 with a key that matches `^dalan_[A-Za-z0-9_-]{43}$`, `expires_at` 90 days from now, and the audit log has `key.create`

#### Scenario: Only the hash is stored
- **WHEN** a key is made
- **THEN** no column of `identity.access_keys` contains the key, and the list shows the name but not the key

#### Scenario: Too many keys
- **GIVEN** `Identity:MaxAccessKeys` is 1 and the user has one key
- **WHEN** the user makes another
- **THEN** the answer is 409 `key_limit`

### Requirement: AUTH-23 List and delete access keys
With a session, `GET /api/account/keys` SHALL list the user's keys (`id`, `name`, `created_at`, `expires_at`) without the key. `DELETE /api/account/keys/{id}` SHALL delete one of the user's keys, else 404 `key_not_found`; the audit log gets `key.delete`. The three key calls SHALL NOT accept an access key.

#### Scenario: Delete
- **GIVEN** a user with a key
- **WHEN** the user deletes it
- **THEN** it is not in the list, a call with it gets 401, and the audit log has `key.delete`

#### Scenario: Another user's key
- **WHEN** user B deletes user A's key
- **THEN** the answer is 404 `key_not_found` and the key still works

#### Scenario: A key cannot make keys
- **WHEN** `POST /api/account/keys` is called with only an access key
- **THEN** the answer is 401

### Requirement: AUTH-22 Where an access key works
A call with `Authorization: Bearer <access key>` SHALL be served as the key's user only at the builder calls (`/api/builder/...`) and the MCP door (`/api/mcp`). Any other call with only a key SHALL get 401 `unauthorized`. An unknown, deleted or expired key, or a key of a suspended user, SHALL get 401 `unauthorized`. The session token SHALL be read only from its cookie, never from the `Authorization` header.

#### Scenario: Builder call
- **GIVEN** an approved builder's key
- **WHEN** `GET /api/builder/bots` is called with it
- **THEN** the answer is 200 with the builder's bots

#### Scenario: Not a builder door
- **WHEN** `GET /api/me` or `GET /api/chats` is called with only a key
- **THEN** the answer is 401

#### Scenario: Expired
- **GIVEN** a key made 91 days ago
- **WHEN** it is used
- **THEN** the answer is 401

#### Scenario: Suspended user
- **GIVEN** a builder's key
- **WHEN** an admin suspends the builder
- **THEN** the next call with the key gets 401

#### Scenario: Session token in the header
- **WHEN** a valid session token is sent as `Authorization: Bearer` with no cookie
- **THEN** the answer is 401

### Requirement: AUTH-24 Ask for an email code
`POST /api/auth/email` with `email`, `challenge` and `nonce` (SEC-06) SHALL make a random code of `Identity:EmailCodeDigits` digits (start 6), store only a keyed hash of it, send it through `IEmailSender` (NTF-10), and answer 200 with `expires_in`, `retry_in` and `code_digits`. An address not like `name@host.tld`, or over 254 characters, SHALL get 400 `bad_email`. A failed send SHALL get 503 `email_unavailable` and not count. A suspended user gets 403 `suspended`.

#### Scenario: Code is sent
- **WHEN** a client asks a code for `Sara@Example.com ` with a valid proof
- **THEN** one email with a 6-digit code goes to `sara@example.com` and the response is 200 with `expires_in` 600 and `retry_in` 60

#### Scenario: Code is not stored in clear
- **WHEN** a code was sent
- **THEN** no column of `identity.email_codes` holds the code

#### Scenario: Not an email
- **WHEN** a client asks a code for `sara.example.com`
- **THEN** the response is 400 `bad_email` and no email is sent

#### Scenario: Sender fails
- **GIVEN** the email sender fails
- **WHEN** a client asks a code
- **THEN** the response is 503 `email_unavailable`, and a new try right after it is not refused by the wait

### Requirement: AUTH-25 Log in with the email code
`POST /api/auth/email/verify` with `email` and `code` SHALL log the user of that address in under the rules of AUTH-12, with `Identity:EmailCodeSeconds` (start 600) and `Identity:EmailCodeTries` (start 5); any other case gets 400 `wrong_code`. An address with no user yet SHALL need a `name` (else 400 `name_required`, and the code stays valid); with it a new user with that email and no phone SHALL be made. A suspended user SHALL get 403 `suspended`.

#### Scenario: New address with a name
- **GIVEN** an address with no user, and the answer `name_required`
- **WHEN** the client posts the same code again with the name "Sara"
- **THEN** a user "Sara" with that email and no phone exists and the response sets both cookies

#### Scenario: Known user
- **GIVEN** a user with the address and a code that was just sent
- **WHEN** the client posts the address and the code
- **THEN** the response is 200 with the user's profile

#### Scenario: Wrong code
- **WHEN** the client posts a wrong code
- **THEN** the response is 400 `wrong_code` and no cookie is set

#### Scenario: Expired code
- **GIVEN** a code sent more than `Identity:EmailCodeSeconds` ago
- **WHEN** the client posts it
- **THEN** the response is 400 `wrong_code`

#### Scenario: Used once
- **GIVEN** a code that logged the user in
- **WHEN** the same code is posted again
- **THEN** the response is 400 `wrong_code`

### Requirement: AUTH-26 Add a phone number (level 2)
With a session, `POST /api/account/phone` with `phone` and `code` SHALL check the SMS code (the code is asked with `POST /api/auth/sms`, the same rules as AUTH-12). When it is right and no account has the phone, it SHALL save the phone on the user and answer 200 with the profile (`has_phone` true). When it is right and another account has the phone, it SHALL make a pending merge (AUTH-31) and answer 409 `merge_needed`. A wrong code SHALL get 400 `wrong_code`. A user who has a phone SHALL get 409 `has_phone`. The call SHALL NOT accept an access key.

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

### Requirement: AUTH-27 Start the GitHub login
When GitHub login is on (AUTH-16), `GET /api/auth/github` SHALL set the cookie `__Host-dalan_github` (`HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, `Max-Age` `Identity:GitHub:StateSeconds`, start 600) to a new random state of 256 bits, and answer 302 to `<Identity:GitHub:WebUrl>/login/oauth/authorize` with `client_id`, `redirect_uri` (`Identity:GitHub:CallbackUrl`), `scope=user:email` and the same `state`. When GitHub login is off it SHALL answer 404.

#### Scenario: Go to GitHub
- **WHEN** a browser opens `/api/auth/github`
- **THEN** the answer is 302 to GitHub's authorize address with the client id, the callback URL, scope `user:email` and a state equal to the new cookie

#### Scenario: Off
- **GIVEN** no GitHub client id is set
- **WHEN** a browser opens `/api/auth/github`
- **THEN** the answer is 404

### Requirement: AUTH-28 Come back from GitHub
`GET /api/auth/github/callback` with `code` and the right `state` (AUTH-29) SHALL read the GitHub user and its primary verified email, and log in (AUTH-03) the user with that GitHub id; else the user with that email, saving the GitHub id on it; else a new user with that email, GitHub id and GitHub name (or login), and no phone. It SHALL record the caller in the access log (ADM-01) and answer 302 to `/`. The GitHub token SHALL NOT be stored.

#### Scenario: New user
- **GIVEN** a GitHub account 42 named "Sara Rad" with the primary verified email `Sara@Example.com`, and no Dalan user with it
- **WHEN** the browser comes back with a code and the right state
- **THEN** the answer is 302 to `/` with both session cookies, and a user "Sara Rad" with email `sara@example.com`, GitHub id 42 and no phone exists

#### Scenario: Same email as an email user
- **GIVEN** a user who logged in by email code with `sara@example.com`
- **WHEN** GitHub account 42 with that verified email comes back
- **THEN** that same user is logged in and now has GitHub id 42

#### Scenario: Known GitHub account
- **GIVEN** a user with GitHub id 42, and GitHub now says the email is `new@example.com`
- **WHEN** GitHub account 42 comes back
- **THEN** that same user is logged in and no new user is made

#### Scenario: Access log
- **WHEN** GitHub account 42 logs in from `203.0.113.7` port `51000`
- **THEN** the access log has a row for that user with `203.0.113.7` and `51000`

### Requirement: AUTH-29 GitHub login failures
The callback SHALL delete the state cookie on every call. Each failure SHALL answer 302 to `/?login_error=<code>` and set no session: `github_state` (no state, or not equal to the cookie), `github_denied` (the person said no at GitHub), `github_no_email` (no primary verified email), `github_unavailable` (no answer after `Identity:GitHub:Tries` tries, start 3, of `Identity:GitHub:TimeoutSeconds` each, start 10, or the code was refused), `suspended`.

#### Scenario: Wrong state
- **WHEN** the browser comes back with a state that is not the cookie's, or with no cookie
- **THEN** the answer is 302 to `/?login_error=github_state`, GitHub is not called and no session cookie is set

#### Scenario: Person said no
- **WHEN** the browser comes back with `error=access_denied`
- **THEN** the answer is 302 to `/?login_error=github_denied`

#### Scenario: No verified email
- **GIVEN** a GitHub account whose emails are not verified
- **WHEN** it comes back
- **THEN** the answer is 302 to `/?login_error=github_no_email` and no user is made

#### Scenario: GitHub cannot be reached
- **GIVEN** GitHub does not answer (international links are cut)
- **WHEN** the browser comes back
- **THEN** the answer is 302 to `/?login_error=github_unavailable`

#### Scenario: Suspended
- **GIVEN** the user of GitHub account 42 is suspended
- **WHEN** it comes back
- **THEN** the answer is 302 to `/?login_error=suspended` and no session cookie is set

#### Scenario: The link stalls once
- **GIVEN** the first call to GitHub gets no answer and the next one does
- **WHEN** the browser comes back
- **THEN** the user is logged in

#### Scenario: GitHub answers with an error
- **GIVEN** GitHub refuses the code
- **WHEN** the browser comes back
- **THEN** GitHub is called once for the token and the answer is 302 to `/?login_error=github_unavailable`

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
