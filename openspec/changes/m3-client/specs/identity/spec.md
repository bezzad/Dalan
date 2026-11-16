## ADDED Requirements

### Requirement: AUTH-03 Session cookie
A login SHALL give the browser a cookie named `__Host-dalan_session` with the flags `HttpOnly`, `Secure`, `SameSite=Strict` and `Path=/`. Its value SHALL be a JWT signed with ES256 that carries the user id and expires after `Identity:AccessTokenMinutes` (start 15). A missing, expired, changed or wrongly signed token SHALL NOT be accepted.

#### Scenario: Cookie flags
- **WHEN** a user logs in
- **THEN** the response sets `__Host-dalan_session` with `HttpOnly`, `Secure`, `SameSite=Strict` and `Path=/`

#### Scenario: Token is ES256
- **WHEN** a user logs in
- **THEN** the cookie value is a JWT whose header says `ES256` and whose `sub` is the user id

#### Scenario: Expired
- **GIVEN** a logged-in user
- **WHEN** the lifetime passes and the user calls `GET /api/me`
- **THEN** the response is 401 `unauthorized`

#### Scenario: Changed token
- **WHEN** a call carries a session cookie whose user id was changed
- **THEN** the response is 401 `unauthorized`

### Requirement: AUTH-04 Who am I
`GET /api/me` SHALL return the `id`, `name` and `username` of the logged-in user, and never the phone number. Without a session it SHALL answer 401 `unauthorized`.

#### Scenario: Logged in
- **WHEN** a logged-in user calls `GET /api/me`
- **THEN** the response is 200 with the user's id and name, and the body does not contain the phone number

#### Scenario: Not logged in
- **WHEN** a call to `GET /api/me` has no session cookie
- **THEN** the response is 401 `unauthorized`

### Requirement: AUTH-05 Logout
`POST /api/logout` SHALL remove the session cookie from the browser.

#### Scenario: Log out
- **GIVEN** a logged-in user
- **WHEN** the user calls `POST /api/logout` and then `GET /api/me`
- **THEN** the second response is 401

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
