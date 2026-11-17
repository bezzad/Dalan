## MODIFIED Requirements

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
`GET /api/me` SHALL return the `id`, `name`, `username` and `has_password` of the logged-in user, and never the phone number or the password hash. Without a session it SHALL answer 401 `unauthorized`.

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

## ADDED Requirements

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
`POST /api/account/password` with `password` SHALL store an Argon2id hash of it (PHC text with its own salt and cost) and answer 204. A password shorter than `Identity:PasswordMinLength` (start 8) or longer than `Identity:PasswordMaxLength` (start 128) SHALL get 400 `weak_password`. When the user has a password and the session did not start with an SMS code, the call SHALL need the right `current_password`, else 403 `wrong_password`. A changed password SHALL end every other session of the user.

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

#### Scenario: Other devices are logged out
- **GIVEN** a user logged in on two devices
- **WHEN** the first device changes the password
- **THEN** the first device stays logged in and a refresh on the second device is 401

#### Scenario: Not logged in
- **WHEN** the call has no session
- **THEN** the response is 401 `unauthorized`

### Requirement: AUTH-14 Log in with a password
`POST /api/auth/password` with `login` (the phone number or the username) and `password` SHALL log the user in (AUTH-03, AUTH-08) when the password is right. An unknown login, a user with no password and a wrong password SHALL all get the same answer: 401 `wrong_login`.

#### Scenario: By phone
- **GIVEN** a user with phone `+989121234567` and a password
- **WHEN** the client posts `{"login":"09121234567","password":"<right>"}`
- **THEN** the response is 200 with the user's profile and sets both cookies

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
`GET /api/auth/settings` SHALL need no session and SHALL return `dev_login` (is the development login on), `password_min_length` and `name_max_length`, so the client hard-codes none of them.

#### Scenario: Read
- **WHEN** a client with no session calls `GET /api/auth/settings`
- **THEN** the response is 200 with `dev_login`, `password_min_length` 8 and `name_max_length` 64

### Requirement: AUTH-17 Security events are logged
The server SHALL write a log line for each login, failed login, sent SMS, refused SMS, reuse of an old refresh token and password change, with the event name and the user id when it is known. No log line SHALL hold an SMS code, a password, a token or a whole phone number. The only exception is the `log` SMS sender (NTF-01), which exists only in Development.

#### Scenario: Login is logged
- **WHEN** a user logs in by SMS code
- **THEN** the log has a line with the event `login` and the user id

#### Scenario: Failed login is logged
- **WHEN** a client posts a wrong password
- **THEN** the log has a line with the event `login_failed`

#### Scenario: No secret in the log
- **WHEN** a user asks a code, logs in with it and sets a password
- **THEN** no log line holds the code, the password or the whole phone number
